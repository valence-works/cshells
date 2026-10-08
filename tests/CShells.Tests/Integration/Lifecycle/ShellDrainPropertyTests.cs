using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.Lifecycle;

/// <summary>
/// Tests for <see cref="IShell.Drain"/> — the per-generation drain reference exposed on the
/// <see cref="IShell"/> abstraction (009 / FR-004). Locks in the state-binding invariants and
/// the publish-once contract that <see cref="IShellRegistry.DrainAsync"/> consumes for
/// idempotency.
/// </summary>
public class ShellDrainPropertyTests
{
    [Fact(DisplayName = "Drain is null on a freshly-active Shell (no drain in flight)")]
    public async Task Drain_IsNull_BeforeDrainStarts()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblies()
            .AddShell("alpha", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("alpha");

        Assert.Equal(ShellLifecycleState.Active, shell.State);
        Assert.Null(shell.Drain);
    }

    [Fact(DisplayName = "Drain is non-null while state is Deactivating/Draining/Drained")]
    public async Task Drain_IsNonNull_DuringDeactivatingDrainingDrained()
    {
        var gate = new BlockingDrainGate();
        await using var host = ShellRegistryActivateTests.BuildHost(cshells =>
        {
            cshells.Services.AddSingleton(gate);
            cshells
                .WithAssemblyContaining<ShellDrainPropertyTests>()
                .AddShell("beta", shell => shell.WithFeature<BlockingDrainFeature>());
        });
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("beta");

        var op = await registry.DrainAsync(shell);
        try
        {
            await gate.Started.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.NotNull(shell.Drain);
            Assert.Same(op, shell.Drain);
        }
        finally
        {
            gate.Release();
        }
        var result = await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(DrainStatus.Completed, result.Status);
    }

    [Fact(DisplayName = "Drain is null after the shell reaches Disposed")]
    public async Task Drain_IsNull_AfterDispose()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblies()
            .AddShell("gamma", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("gamma");

        var op = await registry.DrainAsync(shell);
        await op.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        // The drain run drives the shell to Disposed. Per the FR-004 invariant, Drain is
        // hidden after that transition. Its internal operation is released on completion.
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
        Assert.Null(shell.Drain);
    }

    [Fact(DisplayName = "Drain returns the same instance as IShellRegistry.DrainAsync")]
    public async Task Drain_SameInstance_AsRegistryDrainAsyncReturn()
    {
        var gate = new BlockingDrainGate();
        await using var host = ShellRegistryActivateTests.BuildHost(cshells =>
        {
            cshells.Services.AddSingleton(gate);
            cshells
                .WithAssemblyContaining<ShellDrainPropertyTests>()
                .AddShell("delta", shell => shell.WithFeature<BlockingDrainFeature>());
        });
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("delta");

        var fromRegistry = await registry.DrainAsync(shell);
        try
        {
            await gate.Started.WaitAsync(TimeSpan.FromSeconds(5));
            var fromShell = shell.Drain;

            Assert.NotNull(fromShell);
            Assert.Same(fromRegistry, fromShell);
        }
        finally
        {
            gate.Release();
        }

        await fromRegistry.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "Drain is the same instance across concurrent DrainAsync calls (publish-once CAS)")]
    public async Task Drain_SameInstance_AcrossConcurrentDrainAsyncCalls()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblies()
            .AddShell("epsilon", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("epsilon");

        // Race 16 concurrent DrainAsync calls. The first to win the CAS publishes its
        // DrainOperation onto the shell; every subsequent caller observes the same instance.
        var tasks = Enumerable
            .Range(0, 16)
            .Select(_ => registry.DrainAsync(shell))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        var first = results[0];
        Assert.All(results, r => Assert.Same(first, r));

        // Don't assert shell.Drain here — with no handlers the drain can complete and dispose
        // the shell before Task.WhenAll returns. The CAS contract is
        // already proven by Assert.All above; shell.Drain identity is covered by
        // Drain_SameInstance_AsRegistryDrainAsyncReturn.
        await first.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    public sealed class BlockingDrainFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) =>
            services.AddTransient<IDrainHandler, BlockingDrainHandler>();
    }

    private sealed class BlockingDrainHandler(BlockingDrainGate gate) : IDrainHandler
    {
        public async Task DrainAsync(IDrainExtensionHandle _, CancellationToken ct)
        {
            gate.SignalStarted();
            await gate.Released.WaitAsync(ct);
        }
    }

    private sealed class BlockingDrainGate
    {
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => started.Task;

        public Task Released => released.Task;

        public void SignalStarted() => started.TrySetResult();

        public void Release() => released.TrySetResult();
    }
}
