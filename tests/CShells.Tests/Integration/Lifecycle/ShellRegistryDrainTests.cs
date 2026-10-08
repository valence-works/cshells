using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using CShells.Lifecycle.Policies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CShells.Tests.Integration.Lifecycle;

public class ShellRegistryDrainTests
{
    [Fact(DisplayName = "DrainAsync with zero handlers completes immediately (SC-009)")]
    public async Task Drain_WithNoHandlers_CompletesImmediately()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryDrainTests>()
            .AddShell("plain", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("plain");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DrainStatus.Completed, result.Status);
        Assert.Empty(result.HandlerResults);
        Assert.Equal(0, result.AbandonedScopeCount);
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
    }

    [Theory(DisplayName = "DrainAsync invokes 0/1/50 handlers in parallel (SC-004)")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    public async Task Drain_InvokesHandlersInParallel(int handlerCount)
    {
        RecordingDrainFeature.HandlerCount = handlerCount;
        var hits = RecordingDrainFeature.Hits = [];

        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryDrainTests>()
            .AddShell("workflow", s => s.WithFeature<RecordingDrainFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("workflow");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DrainStatus.Completed, result.Status);
        Assert.Equal(handlerCount, result.HandlerResults.Count);
        Assert.Equal(handlerCount, hits.Count);
        Assert.All(result.HandlerResults, r => Assert.True(r.Completed));
        Assert.All(result.HandlerResults, r => Assert.Null(r.Error));
    }

    [Fact(DisplayName = "Initializer ordering does not change parallel drain handler invocation")]
    public async Task Drain_WithOrderedInitializer_StillInvokesHandlersInParallel()
    {
        ParallelDrainFeature.Reset();

        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.AddCShells(cshells =>
        {
            cshells.WithAssemblyContaining<ShellRegistryDrainTests>();
            cshells.AddShell("parallel", s => s.WithFeature<ParallelDrainFeature>());
            cshells.Services.Replace(ServiceDescriptor.Singleton<IDrainPolicy>(_ => new FixedTimeoutDrainPolicy(TimeSpan.FromMilliseconds(750))));
            cshells.Services.Replace(ServiceDescriptor.Singleton(new DrainGracePeriod(TimeSpan.FromMilliseconds(50))));
        });

        await using var host = services.BuildServiceProvider();
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("parallel");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DrainStatus.Completed, result.Status);
        Assert.True(ParallelDrainFeature.Initialized);
        Assert.Equal(2, ParallelDrainFeature.Entered);
        Assert.All(result.HandlerResults, r => Assert.True(r.Completed));
    }

    [Fact(DisplayName = "Throwing handler is captured in DrainHandlerResult.Error without aborting peers")]
    public async Task Drain_ThrowingHandler_DoesNotAbortPeers()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryDrainTests>()
            .AddShell("mixed", s => s.WithFeature<MixedDrainFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("mixed");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, result.HandlerResults.Count);
        var good = result.HandlerResults.Single(r => r.HandlerTypeName == nameof(GoodDrainHandler));
        var bad = result.HandlerResults.Single(r => r.HandlerTypeName == nameof(ThrowingDrainHandler));
        Assert.True(good.Completed);
        Assert.Null(good.Error);
        Assert.False(bad.Completed);
        Assert.IsType<ApplicationException>(bad.Error);
    }

    [Fact(DisplayName = "Concurrent DrainAsync for the same shell returns the same operation (FR-028, SC-006)")]
    public async Task Drain_Concurrent_ReturnsSameOperation()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryDrainTests>()
            .AddShell("slow", s => s.WithFeature<SlowDrainFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("slow");

        var ops = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => registry.DrainAsync(shell)));

        Assert.All(ops, o => Assert.Same(ops[0], o));
        await ops[0].WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "Completed drain entries are pruned from the registry's tracking dictionary")]
    public async Task Drain_AfterCompletion_EntryIsPruned()
    {
        // Regression for greptile P1 on PR #86: _drainOps must not retain completed DrainOperation
        // entries — otherwise long-running hosts leak one drained Shell + DrainOperation + TCS + CTS
        // per reload. Behavioural probe: once the first drain finishes, a second DrainAsync call for
        // the same shell must produce a fresh operation (proving the old entry was removed).
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryDrainTests>()
            .AddShell("plain", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("plain");

        var firstOp = await registry.DrainAsync(shell);
        await firstOp.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        var secondOp = await registry.DrainAsync(shell);

        Assert.NotSame(firstOp, secondOp);
        Assert.Null(shell.Drain);
        var secondResult = await secondOp.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(DrainStatus.Completed, secondResult.Status);
        Assert.Empty(secondResult.HandlerResults);
        Assert.Null(shell.Drain);

        var thirdOp = await registry.DrainAsync(shell);
        Assert.NotSame(secondOp, thirdOp);
        await thirdOp.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "Fixed-timeout policy cancels handler after deadline → TimedOut status")]
    public async Task Drain_FixedTimeout_TimesOut()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryDrainTests>()
            .AddShell("stuck", s => s.WithFeature<StuckDrainFeature>()));

        // Override drain policy to 150 ms for this test.
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.AddCShells(cshells =>
        {
            cshells.WithAssemblyContaining<ShellRegistryDrainTests>();
            cshells.AddShell("stuck", s => s.WithFeature<StuckDrainFeature>());
            cshells.Services.Replace(ServiceDescriptor.Singleton<IDrainPolicy>(_ => new FixedTimeoutDrainPolicy(TimeSpan.FromMilliseconds(150))));
            cshells.Services.Replace(ServiceDescriptor.Singleton(new DrainGracePeriod(TimeSpan.FromMilliseconds(200))));
        });

        await using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("stuck");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DrainStatus.TimedOut, result.Status);
        Assert.False(result.HandlerResults[0].Completed);
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
    }

    [Fact(DisplayName = "Drain timeout remains unchanged when a feature uses ordered initializers")]
    public async Task Drain_WithOrderedInitializer_FixedTimeoutStillTimesOut()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.AddCShells(cshells =>
        {
            cshells.WithAssemblyContaining<ShellRegistryDrainTests>();
            cshells.AddShell("stuck", s => s.WithFeature<OrderedInitializerStuckDrainFeature>());
            cshells.Services.Replace(ServiceDescriptor.Singleton<IDrainPolicy>(_ => new FixedTimeoutDrainPolicy(TimeSpan.FromMilliseconds(150))));
            cshells.Services.Replace(ServiceDescriptor.Singleton(new DrainGracePeriod(TimeSpan.FromMilliseconds(200))));
        });

        await using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("stuck");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DrainStatus.TimedOut, result.Status);
        Assert.False(result.HandlerResults[0].Completed);
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
    }

    // =================================================================
    // Test doubles
    // =================================================================

    public sealed class RecordingDrainFeature : IShellFeature
    {
        public static int HandlerCount;
        public static System.Collections.Concurrent.ConcurrentBag<int> Hits = [];

        public void ConfigureServices(IServiceCollection services)
        {
            for (var i = 0; i < HandlerCount; i++)
                services.AddTransient<IDrainHandler, RecordingDrainHandler>();
        }
    }

    private sealed class RecordingDrainHandler : IDrainHandler
    {
        public Task DrainAsync(IDrainExtensionHandle _, CancellationToken ct)
        {
            RecordingDrainFeature.Hits.Add(Environment.CurrentManagedThreadId);
            return Task.CompletedTask;
        }
    }

    public sealed class ParallelDrainFeature : IShellFeature
    {
        public static bool Initialized;
        public static int Entered;
        public static TaskCompletionSource BothEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static void Reset()
        {
            Initialized = false;
            Entered = 0;
            BothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddShellInitializer<NoopDrainInitializer>(LifecyclePhase.Prepare, 0);
            services.AddTransient<IDrainHandler, CoordinatedDrainHandler>();
            services.AddTransient<IDrainHandler, CoordinatedDrainHandler>();
        }
    }

    private sealed class NoopDrainInitializer : IShellInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            ParallelDrainFeature.Initialized = true;
            return Task.CompletedTask;
        }
    }

    private sealed class CoordinatedDrainHandler : IDrainHandler
    {
        public async Task DrainAsync(IDrainExtensionHandle _, CancellationToken ct)
        {
            if (Interlocked.Increment(ref ParallelDrainFeature.Entered) == 2)
                ParallelDrainFeature.BothEntered.SetResult();

            await ParallelDrainFeature.BothEntered.Task.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    public sealed class MixedDrainFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IDrainHandler, GoodDrainHandler>();
            services.AddTransient<IDrainHandler, ThrowingDrainHandler>();
        }
    }

    private sealed class GoodDrainHandler : IDrainHandler
    {
        public Task DrainAsync(IDrainExtensionHandle _, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ThrowingDrainHandler : IDrainHandler
    {
        public Task DrainAsync(IDrainExtensionHandle _, CancellationToken ct) => throw new ApplicationException("boom");
    }

    public sealed class SlowDrainFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) =>
            services.AddTransient<IDrainHandler, SlowDrainHandler>();
    }

    private sealed class SlowDrainHandler : IDrainHandler
    {
        public Task DrainAsync(IDrainExtensionHandle _, CancellationToken ct) => Task.Delay(200, ct);
    }

    public sealed class StuckDrainFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) =>
            services.AddTransient<IDrainHandler, StuckDrainHandler>();
    }

    private sealed class StuckDrainHandler : IDrainHandler
    {
        public async Task DrainAsync(IDrainExtensionHandle _, CancellationToken ct)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }
    }

    public sealed class OrderedInitializerStuckDrainFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddShellInitializer<StuckDrainPrepareInitializer>(LifecyclePhase.Prepare, 0);
            services.AddTransient<IDrainHandler, StuckDrainHandler>();
        }
    }

    private sealed class StuckDrainPrepareInitializer : IShellInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
