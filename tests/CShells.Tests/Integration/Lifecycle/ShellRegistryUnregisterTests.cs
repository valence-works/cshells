using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using CShells.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.Lifecycle;

public class ShellRegistryUnregisterTests
{
    [Fact(DisplayName = "GetManagerAsync returns the manager for a mutable-owned name")]
    public async Task GetManager_Mutable_ReturnsManager()
    {
        var manager = new StubShellBlueprintManager();
        var stub = new StubShellBlueprintProvider().Add("acme-43", manager: manager);
        await using var host = BuildHostWith(stub);
        var registry = host.GetRequiredService<IShellRegistry>();

        var resolved = await registry.GetManagerAsync("acme-43");

        Assert.Same(manager, resolved);
    }

    [Fact(DisplayName = "GetManagerAsync returns null for a read-only-owned name")]
    public async Task GetManager_ReadOnly_ReturnsNull()
    {
        var stub = new StubShellBlueprintProvider().Add("built-in");  // no manager
        await using var host = BuildHostWith(stub);
        var registry = host.GetRequiredService<IShellRegistry>();

        var resolved = await registry.GetManagerAsync("built-in");

        Assert.Null(resolved);
    }

    [Fact(DisplayName = "UnregisterBlueprintAsync calls manager.DeleteAsync before draining the active generation")]
    public async Task Unregister_DeleteFirst_ThenDrain()
    {
        var manager = new StubShellBlueprintManager();
        var stub = new StubShellBlueprintProvider().Add("acme-43", manager: manager);
        await using var host = BuildHostWith(stub);
        var registry = host.GetRequiredService<IShellRegistry>();

        var shell = await registry.GetOrActivateAsync("acme-43");
        Assert.Equal(ShellLifecycleState.Active, shell.State);

        await registry.UnregisterBlueprintAsync("acme-43");

        // Delete ran first (manager op recorded), then shell was drained.
        var ops = manager.Operations.ToArray();
        Assert.Single(ops);
        Assert.Equal(("Delete", "acme-43"), ops[0]);
        Assert.Equal(ShellLifecycleState.Disposed, shell.State);
        Assert.Null(registry.GetActive("acme-43"));
    }

    [Fact(DisplayName = "UnregisterBlueprintAsync with no manager throws BlueprintNotMutableException; runtime state untouched")]
    public async Task Unregister_NoManager_Throws_PreservesRuntime()
    {
        var stub = new StubShellBlueprintProvider().Add("built-in");  // read-only
        await using var host = BuildHostWith(stub);
        var registry = host.GetRequiredService<IShellRegistry>();

        var shell = await registry.GetOrActivateAsync("built-in");
        Assert.Equal(ShellLifecycleState.Active, shell.State);

        await Assert.ThrowsAsync<BlueprintNotMutableException>(
            () => registry.UnregisterBlueprintAsync("built-in"));

        Assert.Equal(ShellLifecycleState.Active, shell.State);
        Assert.Same(shell, registry.GetActive("built-in"));
    }

    [Fact(DisplayName = "UnregisterBlueprintAsync: manager delete fails → exception propagates, runtime state unchanged")]
    public async Task Unregister_DeleteFails_RuntimeUnchanged()
    {
        var manager = new StubShellBlueprintManager { ThrowOnDelete = new InvalidOperationException("db down") };
        var stub = new StubShellBlueprintProvider().Add("acme-43", manager: manager);
        await using var host = BuildHostWith(stub);
        var registry = host.GetRequiredService<IShellRegistry>();

        var shell = await registry.GetOrActivateAsync("acme-43");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.UnregisterBlueprintAsync("acme-43"));

        // Active generation still serving.
        Assert.Equal(ShellLifecycleState.Active, shell.State);
        Assert.Same(shell, registry.GetActive("acme-43"));
    }

    [Fact(DisplayName = "UnregisterBlueprintAsync for unknown name throws ShellBlueprintNotFoundException")]
    public async Task Unregister_Unknown_NotFound()
    {
        var stub = new StubShellBlueprintProvider();
        await using var host = BuildHostWith(stub);
        var registry = host.GetRequiredService<IShellRegistry>();

        await Assert.ThrowsAsync<ShellBlueprintNotFoundException>(
            () => registry.UnregisterBlueprintAsync("missing"));
    }

    [Fact(DisplayName = "After Unregister, GetOrActivateAsync for the same name throws ShellBlueprintNotFoundException")]
    public async Task Unregister_ThenActivate_NotFound()
    {
        // Couple the manager's delete to the stub provider's state so the flow mirrors a real
        // persistent-delete: manager.DeleteAsync removes the blueprint from the provider's view.
        StubShellBlueprintProvider stub = null!;
        var manager = new CouplingManager(() => stub.Clear());
        stub = new StubShellBlueprintProvider().Add("temp", manager: manager);
        await using var host = BuildHostWith(stub);
        var registry = host.GetRequiredService<IShellRegistry>();

        await registry.GetOrActivateAsync("temp");
        await registry.UnregisterBlueprintAsync("temp");

        await Assert.ThrowsAsync<ShellBlueprintNotFoundException>(
            () => registry.GetOrActivateAsync("temp"));
    }

    private sealed class CouplingManager(Action onDelete) : IShellBlueprintManager
    {
        public bool Owns(string name) => true;
        public Task CreateAsync(ShellSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(ShellSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string name, CancellationToken ct = default) { onDelete(); return Task.CompletedTask; }
    }

    [Fact(DisplayName = "After manager.Create + GetOrActivate, the new blueprint activates as generation 1")]
    public async Task Create_ThenActivate_Generation1()
    {
        var manager = new StubShellBlueprintManager();
        var stub = new StubShellBlueprintProvider();  // empty initially, but manager wired
        await using var host = BuildHostWith(stub);
        var registry = host.GetRequiredService<IShellRegistry>();

        // Simulate persisted create: manager records it; provider also vends it.
        await manager.CreateAsync(new ShellSettings(new ShellId("acme-44")));
        stub.Add("acme-44", manager: manager);

        var shell = await registry.GetOrActivateAsync("acme-44");

        Assert.Equal("acme-44", shell.Descriptor.Name);
        Assert.Equal(1, shell.Descriptor.Generation);
        Assert.Contains(("Create", "acme-44"), manager.Operations);
    }

    [Fact]
    public async Task RecreatedNameReservesNewGenerationCaseInsensitively()
    {
        var stub = new StubShellBlueprintProvider();
        var manager = new CouplingManager(() => stub.Clear());
        var participant = new ContextRecordingParticipant();
        stub.Add("recreated", manager: manager);
        await using var host = BuildHostWith(stub, participant);
        var registry = host.GetRequiredService<IShellRegistry>();

        var first = await registry.ActivateAsync("recreated");
        await registry.UnregisterBlueprintAsync("recreated");
        stub.Add("RECREATED", manager: manager);
        var replacement = await registry.ActivateAsync("RECREATED");

        Assert.Equal(ShellLifecycleState.Disposed, first.State);
        Assert.Equal([1, 2], participant.Contexts.Select(context => context.Descriptor.Generation));
        Assert.Equal(2, replacement.Descriptor.Generation);
        Assert.Same(replacement, registry.GetActive("recreated"));
        var drain = await registry.DrainAsync(replacement);
        await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class ContextRecordingParticipant : IShellGenerationBuildParticipant
    {
        public List<ShellGenerationBuildContext> Contexts { get; } = [];

        public ValueTask<IShellGenerationBuildLease> BeginAsync(ShellGenerationBuildContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            return ValueTask.FromResult<IShellGenerationBuildLease>(new Lease());
        }

        private sealed class Lease : IShellGenerationBuildLease
        {
            public ValueTask OnSnapshotSelectedAsync(RuntimeFeatureCatalogSnapshot snapshot, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private static ServiceProvider BuildHostWith(StubShellBlueprintProvider stub, IShellGenerationBuildParticipant? participant = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        if (participant is not null)
            services.AddSingleton(participant);
        services.AddCShells(cshells =>
        {
            cshells.WithAssemblies();
            cshells.AddBlueprintProvider(_ => stub);
        });
        return services.BuildServiceProvider();
    }
}
