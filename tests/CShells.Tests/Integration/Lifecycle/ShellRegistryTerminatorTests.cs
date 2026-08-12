using System.Collections.Concurrent;
using CShells.DependencyInjection;
using CShells.Features;
using CShells.Hosting;
using CShells.Lifecycle;
using CShells.Lifecycle.Policies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CShells.Tests.Integration.Lifecycle;

public class ShellRegistryTerminatorTests
{
    [Fact(DisplayName = "Terminators run after drain handlers while the shell is Drained")]
    public async Task Terminators_RunAfterDrainHandlers_WhileDrained()
    {
        SequenceRecorder.Reset();

        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryTerminatorTests>()
            .AddShell("ordered", s => s.WithFeature<HandlerThenTerminatorFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("ordered");
        SequenceRecorder.ObservedShell = shell;

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DrainStatus.Completed, result.Status);
        Assert.Equal(["handler", "terminator"], SequenceRecorder.Sequence);
        Assert.Equal(ShellLifecycleState.Drained, SequenceRecorder.StateDuringTermination);
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
    }

    [Fact(DisplayName = "Terminators can resolve and use shell services (container-alive guarantee)")]
    public async Task Terminators_CanUseShellContainer()
    {
        SequenceRecorder.Reset();

        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryTerminatorTests>()
            .AddShell("alive", s => s.WithFeature<ContainerProbeFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("alive");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.TerminatorResults.Single().Completed);
        Assert.Equal("injected:resolved", SequenceRecorder.ProbeOutcome);
    }

    [Fact(DisplayName = "Terminators execute mirror-reversed across phases: Start, Default, Prepare")]
    public async Task Terminators_MirroredPhaseOrdering_AcrossPhases()
    {
        SequenceRecorder.Reset();

        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryTerminatorTests>()
            .AddShell("phased", s => s.WithFeature<PhasedTerminatorFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("phased");

        var op = await registry.DrainAsync(shell);
        await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["start", "default", "prepare"], SequenceRecorder.Sequence);
    }

    [Fact(DisplayName = "A throwing terminator is recorded and does not prevent the rest from running")]
    public async Task Terminator_Throwing_LogsAndContinues()
    {
        SequenceRecorder.Reset();

        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryTerminatorTests>()
            .AddShell("mixed", s => s.WithFeature<ThrowingThenGoodTerminatorFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("mixed");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        // The throwing terminator runs first (Start phase); the good one still runs after it.
        Assert.Equal(["good"], SequenceRecorder.Sequence);
        var bad = result.TerminatorResults.Single(r => r.TerminatorTypeName == nameof(ThrowingTerminator));
        var good = result.TerminatorResults.Single(r => r.TerminatorTypeName == nameof(GoodTerminator));
        Assert.False(bad.Completed);
        Assert.IsType<ApplicationException>(bad.Error);
        Assert.True(good.Completed);
        Assert.Null(good.Error);
        Assert.Equal(DrainStatus.Completed, result.Status);
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
    }

    [Fact(DisplayName = "Terminator planning and scope-disposal failures do not fault the drain")]
    public async Task TerminatorPlanningAndScopeDisposalFailures_DoNotFaultDrain()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryTerminatorTests>()
            .AddShell("broken-plan", s => s.WithFeature<BrokenPlanWithThrowingScopeFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("broken-plan");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DrainStatus.Completed, result.Status);
        Assert.Empty(result.TerminatorResults);
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
    }

    [Fact(DisplayName = "Terminators run sequentially, never concurrently")]
    public async Task Terminators_RunSequentially()
    {
        SequenceRecorder.Reset();

        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryTerminatorTests>()
            .AddShell("sequential", s => s.WithFeature<ConcurrencyProbeFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("sequential");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, result.TerminatorResults.Count);
        Assert.All(result.TerminatorResults, r => Assert.True(r.Completed));
        Assert.False(SequenceRecorder.ConcurrencyViolation);
    }

    [Fact(DisplayName = "ReloadAsync runs terminators on the old generation only")]
    public async Task Reload_RunsTerminatorsOnOldGeneration()
    {
        SequenceRecorder.Reset();

        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryTerminatorTests>()
            .AddShell("reloadable", s => s.WithFeature<RecordingTerminatorFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        await registry.ActivateAsync("reloadable");

        var reload = await registry.ReloadAsync("reloadable");
        Assert.NotNull(reload.Drain);
        await reload.Drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, SequenceRecorder.TerminatorHits);
        Assert.Equal(ShellLifecycleState.Active, reload.NewShell!.State);
    }

    [Fact(DisplayName = "UnregisterBlueprintAsync runs terminators before completing")]
    public async Task Unregister_RunsTerminatorsBeforeCompletion()
    {
        SequenceRecorder.Reset();

        // Unregister requires a mutable blueprint source, so use the stub provider + manager.
        var stub = new TestHelpers.StubShellBlueprintProvider()
            .Add("removable", s => s.WithFeature<RecordingTerminatorFeature>(), new TestHelpers.StubShellBlueprintManager());
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryTerminatorTests>()
            .AddBlueprintProvider(_ => stub));
        var registry = host.GetRequiredService<IShellRegistry>();
        await registry.ActivateAsync("removable");

        await registry.UnregisterBlueprintAsync("removable");

        Assert.Equal(1, SequenceRecorder.TerminatorHits);
    }

    [Fact(DisplayName = "Emergency dispose (host shutdown-timeout breach) skips terminators")]
    public async Task EmergencyDispose_SkipsTerminators()
    {
        SequenceRecorder.Reset();

        // Unbounded policy + a handler that never completes: the graceful drain can never
        // reach the terminator phase on its own, so StopAsync's breached shutdown token is
        // the only way the shell gets disposed.
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShells(cshells =>
        {
            cshells.WithAssemblyContaining<ShellRegistryTerminatorTests>();
            cshells.AddShell("stuck", s => s.WithFeature<StuckHandlerWithTerminatorFeature>());
            cshells.Services.Replace(ServiceDescriptor.Singleton<IDrainPolicy>(_ => new UnboundedDrainPolicy()));
        });

        await using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("stuck");

        var hostedService = new CShellsStartupHostedService(registry);
        await hostedService.StopAsync(new CancellationToken(canceled: true));

        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
        Assert.Equal(0, SequenceRecorder.TerminatorHits);
    }

    [Fact(DisplayName = "Emergency disposal during terminator resolution is logged as a warning, not a planning error")]
    public async Task EmergencyDisposeDuringTerminatorResolution_DoesNotLogPlanningError()
    {
        var logs = new CollectingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShells(cshells => cshells
            .WithAssemblyContaining<ShellRegistryTerminatorTests>()
            .AddShell("resolution-race", s => s.WithFeature<DisposeDuringTerminatorResolutionFeature>()));

        await using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("resolution-race");
        SequenceRecorder.ObservedShell = shell;

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DrainStatus.Completed, result.Status);
        Assert.Empty(result.TerminatorResults);
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
        Assert.Contains(logs.Entries, entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("disposed before termination", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Entries, entry =>
            entry.Level >= LogLevel.Error && entry.Message.Contains("terminator planning failed", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Forced drain still runs terminators within the grace budget")]
    public async Task ForcedDrain_StillInvokesTerminatorsWithGraceBudget()
    {
        SequenceRecorder.Reset();

        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryTerminatorTests>()
            .AddShell("forced", s => s.WithFeature<StuckHandlerWithTerminatorFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("forced");

        var op = await registry.DrainAsync(shell);
        await op.ForceAsync();
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(DrainStatus.Forced, result.Status);
        Assert.Equal(1, SequenceRecorder.TerminatorHits);
        Assert.True(result.TerminatorResults.Single().Completed);
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
    }

    [Fact(DisplayName = "ForceAsync during termination cancels the terminator and reports Forced status")]
    public async Task ForceDuringTermination_ReportsForcedStatus()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells =>
        {
            cshells.WithAssemblyContaining<ShellRegistryTerminatorTests>();
            cshells.AddShell("force-terminator", s => s.WithFeature<CancellableTerminatorFeature>());
            cshells.ConfigureGracePeriod(TimeSpan.FromMilliseconds(50));
        });
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("force-terminator");
        var gate = shell.ServiceProvider.GetRequiredService<TerminatorGate>();

        var op = await registry.DrainAsync(shell);
        await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await op.ForceAsync();
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DrainStatus.Forced, result.Status);
        Assert.False(Assert.Single(result.TerminatorResults).Completed);
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
    }

    [Fact(DisplayName = "A hung terminator is abandoned after cancellation + grace; the shell is still disposed")]
    public async Task HungTerminator_AbandonedAfterGrace_ShellStillDisposed()
    {
        SequenceRecorder.Reset();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShells(cshells =>
        {
            cshells.WithAssemblyContaining<ShellRegistryTerminatorTests>();
            cshells.AddShell("hung", s => s.WithFeature<HungTerminatorFeature>());
            cshells.Services.Replace(ServiceDescriptor.Singleton<IDrainPolicy>(_ => new FixedTimeoutDrainPolicy(TimeSpan.FromMilliseconds(150))));
            cshells.Services.Replace(ServiceDescriptor.Singleton(new DrainGracePeriod(TimeSpan.FromMilliseconds(100))));
        });

        await using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("hung");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.TerminatorResults.Single().Completed);
        Assert.Null(result.TerminatorResults.Single().Error);
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
    }

    // =================================================================
    // Test doubles
    // =================================================================

    private static class SequenceRecorder
    {
        public static readonly List<string> Sequence = [];
        public static IShell? ObservedShell;
        public static ShellLifecycleState? StateDuringTermination;
        public static string? ProbeOutcome;
        public static int TerminatorHits;
        public static int CurrentlyRunning;
        public static bool ConcurrencyViolation;

        public static void Reset()
        {
            lock (Sequence)
                Sequence.Clear();
            ObservedShell = null;
            StateDuringTermination = null;
            ProbeOutcome = null;
            TerminatorHits = 0;
            CurrentlyRunning = 0;
            ConcurrencyViolation = false;
        }

        public static void Record(string entry)
        {
            lock (Sequence)
                Sequence.Add(entry);
        }
    }

    private sealed class CollectingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CollectingLogger(Entries);

        public void Dispose() { }

        private sealed class CollectingLogger(ConcurrentQueue<(LogLevel Level, string Message)> entries) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                entries.Enqueue((logLevel, formatter(state, exception)));

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();

                public void Dispose() { }
            }
        }
    }

    public sealed class HandlerThenTerminatorFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IDrainHandler, RecordingHandler>();
            services.AddShellTerminator<StateObservingTerminator>();
        }
    }

    private sealed class RecordingHandler : IDrainHandler
    {
        public Task DrainAsync(IDrainExtensionHandle _, CancellationToken ct)
        {
            SequenceRecorder.Record("handler");
            return Task.CompletedTask;
        }
    }

    private sealed class StateObservingTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default)
        {
            SequenceRecorder.StateDuringTermination = SequenceRecorder.ObservedShell?.State;
            SequenceRecorder.Record("terminator");
            return Task.CompletedTask;
        }
    }

    public sealed class ContainerProbeFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(new ProbeService("injected"));
            services.AddSingleton(new OtherProbeService("resolved"));
            services.AddShellTerminator<ContainerProbeTerminator>();
        }
    }

    private sealed record ProbeService(string Value);

    private sealed record OtherProbeService(string Value);

    private sealed class ContainerProbeTerminator(ProbeService injected, IServiceProvider provider) : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default)
        {
            var resolved = provider.GetRequiredService<OtherProbeService>();
            SequenceRecorder.ProbeOutcome = $"{injected.Value}:{resolved.Value}";
            return Task.CompletedTask;
        }
    }

    public sealed class PhasedTerminatorFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddShellTerminator<PrepareRecordingTerminator>(LifecyclePhase.Prepare, 100);
            services.AddShellTerminator<DefaultRecordingTerminator>(LifecyclePhase.Default, 0);
            services.AddShellTerminator<StartRecordingTerminator>(LifecyclePhase.Start, 100);
        }
    }

    private sealed class PrepareRecordingTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default)
        {
            SequenceRecorder.Record("prepare");
            return Task.CompletedTask;
        }
    }

    private sealed class DefaultRecordingTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default)
        {
            SequenceRecorder.Record("default");
            return Task.CompletedTask;
        }
    }

    private sealed class StartRecordingTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default)
        {
            SequenceRecorder.Record("start");
            return Task.CompletedTask;
        }
    }

    public sealed class ThrowingThenGoodTerminatorFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // Start phase tears down first, so the throwing terminator runs before the good one.
            services.AddShellTerminator<ThrowingTerminator>(LifecyclePhase.Start, 0);
            services.AddShellTerminator<GoodTerminator>(LifecyclePhase.Default, 0);
        }
    }

    private sealed class ThrowingTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default) => throw new ApplicationException("teardown boom");
    }

    private sealed class GoodTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default)
        {
            SequenceRecorder.Record("good");
            return Task.CompletedTask;
        }
    }

    public sealed class BrokenPlanWithThrowingScopeFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<ThrowingDisposeDependency>();
            services.AddTransient<IShellTerminator, DependencyTerminator>();
            services.AddSingleton(new ShellTerminatorRegistration(
                typeof(UnresolvedTerminator),
                LifecyclePhase.Default,
                Order: 0,
                RegistrationIndex: -1,
                IsExplicit: true,
                Source: "test"));
        }
    }

    private sealed class DependencyTerminator(ThrowingDisposeDependency dependency) : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default)
        {
            _ = dependency;
            return Task.CompletedTask;
        }
    }

    private sealed class UnresolvedTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ThrowingDisposeDependency : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.FromException(new ApplicationException("scope disposal failed"));
    }

    public sealed class ConcurrencyProbeFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddShellTerminator<FirstSlowTerminator>();
            services.AddShellTerminator<SecondSlowTerminator>();
        }
    }

    private abstract class SlowTerminatorBase : IShellTerminator
    {
        public async Task TerminateAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref SequenceRecorder.CurrentlyRunning) > 1)
                SequenceRecorder.ConcurrencyViolation = true;
            await Task.Delay(50, cancellationToken);
            Interlocked.Decrement(ref SequenceRecorder.CurrentlyRunning);
        }
    }

    private sealed class FirstSlowTerminator : SlowTerminatorBase;

    private sealed class SecondSlowTerminator : SlowTerminatorBase;

    public sealed class RecordingTerminatorFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) =>
            services.AddShellTerminator<HitCountingTerminator>();
    }

    private sealed class HitCountingTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref SequenceRecorder.TerminatorHits);
            return Task.CompletedTask;
        }
    }

    public sealed class StuckHandlerWithTerminatorFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IDrainHandler, StuckHandler>();
            services.AddShellTerminator<HitCountingTerminator>();
        }
    }

    private sealed class StuckHandler : IDrainHandler
    {
        public Task DrainAsync(IDrainExtensionHandle _, CancellationToken ct) => Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    public sealed class DisposeDuringTerminatorResolutionFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) =>
            services.AddTransient<IShellTerminator>(_ => DisposeShellAndThrow());

        private static IShellTerminator DisposeShellAndThrow()
        {
            ((Shell)SequenceRecorder.ObservedShell!).DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw new ObjectDisposedException(nameof(Shell.ServiceProvider));
        }
    }

    public sealed class CancellableTerminatorFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<TerminatorGate>();
            services.AddShellTerminator<CancellableTerminator>();
        }
    }

    private sealed class TerminatorGate
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class CancellableTerminator(TerminatorGate gate) : IShellTerminator
    {
        public async Task TerminateAsync(CancellationToken cancellationToken = default)
        {
            gate.Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    public sealed class HungTerminatorFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) =>
            services.AddShellTerminator<TokenIgnoringTerminator>();
    }

    private sealed class TokenIgnoringTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default) =>
            Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None);
    }
}
