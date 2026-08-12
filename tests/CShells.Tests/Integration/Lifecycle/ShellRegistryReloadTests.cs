using System.Runtime.CompilerServices;
using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using CShells.Lifecycle.Blueprints;
using CShells.Lifecycle.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.Lifecycle;

public class ShellRegistryReloadTests
{
    [Fact(DisplayName = "ReloadAsync on an inactive name activates generation 1 (FR-011)")]
    public async Task Reload_FirstTime_BehavesLikeActivate()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryReloadTests>()
            .AddShell("payments", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();

        var result = await registry.ReloadAsync("payments");

        Assert.Null(result.Error);
        Assert.NotNull(result.NewShell);
        Assert.Null(result.Drain); // no prior generation
        Assert.Equal(1, result.NewShell!.Descriptor.Generation);
        Assert.Equal(ShellLifecycleState.Active, result.NewShell.State);
    }

    [Fact(DisplayName = "ReloadAsync promotes gen+1 and drains previous generation")]
    public async Task Reload_PromotesNext_AndDrainsPrevious()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryReloadTests>()
            .AddShell("payments", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();

        var gen1 = await registry.ActivateAsync("payments");
        var result = await registry.ReloadAsync("payments");

        Assert.Null(result.Error);
        Assert.NotNull(result.NewShell);
        Assert.NotNull(result.Drain);
        Assert.Equal(2, result.NewShell!.Descriptor.Generation);
        Assert.Same(result.NewShell, registry.GetActive("payments"));

        await result.Drain!.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ShellLifecycleState.Disposed, gen1.State);
        Assert.Equal(ShellLifecycleState.Active, result.NewShell.State);
    }

    [Fact(DisplayName = "ReloadAsync releases the drained previous generation from slot history (no unbounded retention)")]
    public async Task Reload_ReleasesPreviousGeneration_FromHistory()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryReloadTests>()
            .AddShell("payments", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();

        var gen1 = await registry.ActivateAsync("payments");
        Assert.Contains(gen1, registry.GetAll("payments")); // active generation is tracked

        var result = await registry.ReloadAsync("payments");
        await result.Drain!.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ShellLifecycleState.Disposed, gen1.State);
        // Once drained + disposed, the previous generation must no longer be retained by the
        // registry — otherwise slot.All grows by one Shell (and pins its provider + assemblies)
        // on every single reload for the lifetime of the host.
        Assert.DoesNotContain(gen1, registry.GetAll("payments"));
        Assert.Equal([result.NewShell!], registry.GetAll("payments"));
    }

    [Fact(DisplayName = "Drained previous generation is GC-collectible after reload (no leaked strong reference)")]
    public async Task Reload_DrainedGeneration_IsGarbageCollectible()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryReloadTests>()
            .AddShell("payments", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();

        // Activate, reload, and drain inside a non-inlined helper so no local on THIS frame keeps a
        // strong reference to the previous generation once the helper returns.
        var weakGen1 = await ActivateReloadAndDrainAsync(registry);

        for (var i = 0; i < 10 && weakGen1.IsAlive; i++)
        {
            // Yield first: DrainOperation.RunAsync publishes its completion (which the helper's
            // Drain.WaitAsync awaited) before its outer Task.Run finishes unwinding, so a ThreadPool frame
            // may still hold the closure over the shell for a moment after WaitAsync returned. Letting it
            // run makes the reclaim deterministic rather than reliant on GC timing.
            await Task.Yield();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        }

        // If this fails, the registry (or a collaborator) is still rooting the disposed generation.
        // That is exactly the reference that keeps a collectible AssemblyLoadContext (one the
        // generation's assemblies were loaded into) resident, so a forced GC cannot reclaim it.
        Assert.False(weakGen1.IsAlive,
            "The drained previous generation is still rooted after GC — the registry is leaking it.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> ActivateReloadAndDrainAsync(IShellRegistry registry)
    {
        var gen1 = await registry.ActivateAsync("payments");
        var weak = new WeakReference(gen1);

        var result = await registry.ReloadAsync("payments");
        await result.Drain!.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ShellLifecycleState.Disposed, gen1.State);

        // gen1, result (which holds the DrainOperation referencing gen1) and every other local fall out of
        // scope when this helper returns. [MethodImpl(NoInlining)] only stops the JIT folding this frame
        // into the caller; what actually prevents gen1 staying rooted is the runtime clearing the completed
        // async state-machine box's fields — so nothing on the heap holds it either, and only the
        // WeakReference travels back. (The dedicated history test asserts GetAll no longer contains gen1;
        // asserting it here too would mask the GC check by failing first if the release ever regressed.)
        return weak;
    }

    [Fact(DisplayName = "Draining the active generation (no reload) clears GetActive and GetAll together")]
    public async Task DrainActiveGeneration_ClearsActiveAndAll()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryReloadTests>()
            .AddShell("payments", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();

        var active = await registry.ActivateAsync("payments");
        Assert.Same(active, registry.GetActive("payments"));

        // Drain the active generation directly — no reload promotes a replacement, so this is the case
        // where 'Active' would otherwise be left pointing at a Disposed shell.
        var drain = await registry.DrainAsync(active);
        await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ShellLifecycleState.Disposed, active.State);
        // Invariant GetAll ⊇ {GetActive}: once the active generation reaches Disposed it is released from
        // BOTH, so GetActive returns null rather than a Disposed shell that GetAll no longer holds.
        Assert.Null(registry.GetActive("payments"));
        Assert.Empty(registry.GetAll("payments"));
    }

    [Fact(DisplayName = "ReloadAsync with no blueprint throws ShellBlueprintNotFoundException")]
    public async Task Reload_NoBlueprint_Throws()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryReloadTests>());
        var registry = host.GetRequiredService<IShellRegistry>();

        var ex = await Assert.ThrowsAsync<ShellBlueprintNotFoundException>(() => registry.ReloadAsync("unknown"));
        Assert.Equal("unknown", ex.Name);
    }

    [Fact(DisplayName = "Reload composition failure returns ReloadResult.Error and leaves active unchanged (FR-014)")]
    public async Task Reload_CompositionFailure_LeavesActiveUnchanged()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryReloadTests>()
            .AddShell("flaky", _ => { })
            .AddBlueprint(new FailingOnReloadBlueprint("unstable")));
        var registry = host.GetRequiredService<IShellRegistry>();

        var gen1 = await registry.ActivateAsync("flaky");

        // The throwing blueprint was registered upfront — calling Reload triggers its
        // ComposeAsync, which throws and is captured into ReloadResult.Error.
        var result = await registry.ReloadAsync("unstable");

        Assert.NotNull(result.Error);
        Assert.Null(result.NewShell);
        Assert.Null(result.Drain);
        Assert.Null(registry.GetActive("unstable"));

        // Unrelated active is unaffected.
        Assert.Equal(ShellLifecycleState.Active, gen1.State);
        Assert.Same(gen1, registry.GetActive("flaky"));
    }

    [Fact(DisplayName = "Concurrent ReloadAsync for the same name serializes and assigns monotonic generations (FR-013)")]
    public async Task ConcurrentReloads_SerializeMonotonically()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryReloadTests>()
            .AddShell("payments", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();
        await registry.ActivateAsync("payments");

        var reloads = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => registry.ReloadAsync("payments")));

        var generations = reloads
            .Where(r => r.NewShell is not null)
            .Select(r => r.NewShell!.Descriptor.Generation)
            .OrderBy(g => g)
            .ToList();

        Assert.Equal([2, 3, 4, 5, 6, 7, 8, 9], generations);
        Assert.Equal(9, registry.GetActive("payments")!.Descriptor.Generation);
    }

    [Fact(DisplayName = "Reload of different names runs in parallel")]
    public async Task ReloadDifferentNames_RunsInParallel()
    {
        await using var host = ShellRegistryActivateTests.BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryReloadTests>()
            .AddShell("a", _ => { })
            .AddShell("b", _ => { })
            .AddShell("c", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();
        await registry.ActivateAsync("a");
        await registry.ActivateAsync("b");
        await registry.ActivateAsync("c");

        var results = await Task.WhenAll(
            registry.ReloadAsync("a"),
            registry.ReloadAsync("b"),
            registry.ReloadAsync("c"));

        Assert.All(results, r =>
        {
            Assert.Null(r.Error);
            Assert.NotNull(r.NewShell);
            Assert.Equal(2, r.NewShell!.Descriptor.Generation);
        });
    }

    private sealed class FailingOnReloadBlueprint(string name) : IShellBlueprint
    {
        public string Name { get; } = name;
        public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

        public Task<ShellSettings> ComposeAsync(CancellationToken cancellationToken = default)
            => throw new ApplicationException("compose fail");
    }
}
