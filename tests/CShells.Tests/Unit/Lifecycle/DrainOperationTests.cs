using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using CShells.Lifecycle.Policies;
using CShells.Tests.Integration.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CShells.Tests.Unit.Lifecycle;

/// <summary>
/// Unit tests for <see cref="DrainOperation"/> behaviour — force path, grace period timing.
/// Integration-style coverage of the drain flow lives in <c>ShellRegistryDrainTests</c>.
/// </summary>
public class DrainOperationTests
{
    [Fact(DisplayName = "ForceAsync on a pending drain cancels handlers and reports Forced status")]
    public async Task ForceAsync_ReportsForcedStatus()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.AddCShells(cshells =>
        {
            cshells.WithAssemblyContaining<DrainOperationTests>();
            cshells.AddShell("forceable", s => s.WithFeature<StickyFeature>());
            // Generous deadline — we force instead of waiting.
            cshells.ConfigureDrainPolicy(new FixedTimeoutDrainPolicy(TimeSpan.FromMinutes(1)));
            cshells.ConfigureGracePeriod(TimeSpan.FromMilliseconds(200));
        });

        await using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("forceable");

        var op = await registry.DrainAsync(shell);
        // Let the handler start running.
        await Task.Delay(50);

        await op.ForceAsync();
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DrainStatus.Forced, result.Status);
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
    }

    [Fact(DisplayName = "ForceAsync after completion is a no-op")]
    public async Task ForceAsync_After_Completion_IsNoOp()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<DrainOperationTests>()
            .AddShell("quick", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("quick");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(DrainStatus.Completed, result.Status);

        // Force after completion does nothing; status remains Completed.
        await op.ForceAsync();
        Assert.Equal(DrainStatus.Completed, op.Status);
    }

    [Fact(DisplayName = "DrainResult's pre-terminator constructor remains compatible")]
    public void DrainResult_PreTerminatorConstructor_HasEmptyTerminatorResults()
    {
        var result = new DrainResult(
            ShellDescriptor.Create("legacy", 1),
            DrainStatus.Completed,
            TimeSpan.Zero,
            0,
            []);
        var (_, _, _, _, handlerResults) = result;

        Assert.Empty(result.TerminatorResults);
        Assert.Same(result.HandlerResults, handlerResults);
    }

    [Fact(DisplayName = "DrainResult carries the shell's descriptor and the per-handler elapsed timings")]
    public async Task DrainResult_Carries_PerHandler_Details()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<DrainOperationTests>()
            .AddShell("timed", s => s.WithFeature<TimedDrainFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("timed");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(shell.Descriptor, result.Shell);
        var entry = Assert.Single(result.HandlerResults);
        Assert.Equal(nameof(TimedDrainHandler), entry.HandlerTypeName);
        Assert.True(entry.Completed);
        Assert.True(entry.Elapsed >= TimeSpan.FromMilliseconds(30));
    }

    public sealed class StickyFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) =>
            services.AddTransient<IDrainHandler, StickyHandler>();
    }

    private sealed class StickyHandler : IDrainHandler
    {
        public async Task DrainAsync(IDrainExtensionHandle _, CancellationToken ct)
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            catch (OperationCanceledException) { throw; }
        }
    }

    [Fact(DisplayName = "Handler that ignores cancellation and is abandoned after grace period does not crash drain — TimedOut status returned")]
    public async Task AbandonedHandler_AfterGracePeriod_CompletesWithTimedOut()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.AddCShells(cshells =>
        {
            cshells.WithAssemblyContaining<DrainOperationTests>();
            cshells.AddShell("abandoned", s => s.WithFeature<AbandonedFeature>());
            // Short deadline so the handler is signalled quickly, then a short grace period so the
            // abandoned-handler path (results[index] == null before this fix) is exercised fast.
            cshells.ConfigureDrainPolicy(new FixedTimeoutDrainPolicy(TimeSpan.FromMilliseconds(50)));
            cshells.ConfigureGracePeriod(TimeSpan.FromMilliseconds(50));
        });

        await using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("abandoned");

        var op = await registry.DrainAsync(shell);
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DrainStatus.TimedOut, result.Status);
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
        var entry = Assert.Single(result.HandlerResults);
        Assert.Equal(nameof(AbandonedHandler), entry.HandlerTypeName);
        Assert.False(entry.Completed);
    }

    public sealed class TimedDrainFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) =>
            services.AddTransient<IDrainHandler, TimedDrainHandler>();
    }

    private sealed class TimedDrainHandler : IDrainHandler
    {
        public Task DrainAsync(IDrainExtensionHandle _, CancellationToken ct) => Task.Delay(40, ct);
    }

    public sealed class AbandonedFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) =>
            services.AddTransient<IDrainHandler, AbandonedHandler>();
    }

    private sealed class AbandonedHandler : IDrainHandler
    {
        // Deliberately ignores the cancellation token — simulates a stuck handler that does not
        // respect cooperative cancellation. Before the null-seed fix this caused NullReferenceException
        // in ResolveStatus when the slot was never written after the grace period elapsed.
        public Task DrainAsync(IDrainExtensionHandle _, CancellationToken ct) =>
            Task.Delay(Timeout.InfiniteTimeSpan, CancellationToken.None);
    }
}
