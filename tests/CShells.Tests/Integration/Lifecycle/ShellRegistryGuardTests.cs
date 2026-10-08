using CShells.DependencyInjection;
using CShells.Lifecycle;
using CShells.Lifecycle.Blueprints;
using CShells.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.Lifecycle;

/// <summary>
/// Feature 008 US3 + SC-008: composition-time guards (FR-005, FR-006) and the third-party
/// extensibility scenario.
/// </summary>
public class ShellRegistryGuardTests
{
    [Fact(DisplayName = "Mixing AddShell with AddBlueprintProvider throws at composition with a teaching message (FR-005)")]
    public void Mixing_AddShell_With_AddBlueprintProvider_ThrowsAtComposition_WithTeachingMessage()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShells(c => c
            .WithAssemblies()
            .AddShell("platform", _ => { })
            .AddBlueprintProvider(_ => new StubShellBlueprintProvider().Add("acme")));

        using var host = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() => host.GetRequiredService<IShellRegistry>());

        Assert.Contains("AddShell", ex.Message);
        Assert.Contains("AddBlueprintProvider", ex.Message);
        Assert.Contains("exactly one", ex.Message, StringComparison.OrdinalIgnoreCase);
        // Three resolutions enumerated:
        Assert.Contains("(1)", ex.Message);
        Assert.Contains("(2)", ex.Message);
        Assert.Contains("(3)", ex.Message);
    }

    [Fact(DisplayName = "Reverse order — AddBlueprintProvider before AddShell — also throws (registration order doesn't matter)")]
    public void Reverse_Order_AlsoThrows()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShells(c => c
            .WithAssemblies()
            .AddBlueprintProvider(_ => new StubShellBlueprintProvider().Add("acme"))
            .AddShell("platform", _ => { }));

        using var host = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() => host.GetRequiredService<IShellRegistry>());
        Assert.Contains("AddShell", ex.Message);
        Assert.Contains("AddBlueprintProvider", ex.Message);
    }

    [Fact(DisplayName = "Two AddBlueprintProvider calls throw at composition (FR-006)")]
    public void MultipleExternalProviders_ThrowAtComposition()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShells(c => c
            .WithAssemblies()
            .AddBlueprintProvider(_ => new StubShellBlueprintProvider().Add("a"))
            .AddBlueprintProvider(_ => new StubShellBlueprintProvider().Add("b")));

        using var host = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() => host.GetRequiredService<IShellRegistry>());
        Assert.Contains("exactly one external", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Empty host (no AddShell, no AddBlueprintProvider) resolves with empty in-memory provider; lookups return NotFound")]
    public async Task EmptyHost_NoProviderAndNoAddShell_ResolvesEmptyInMemory_NotFoundOnLookup()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShells(c => c.WithAssemblies());

        await using var host = services.BuildServiceProvider();

        var registry = host.GetRequiredService<IShellRegistry>();
        await Assert.ThrowsAsync<ShellBlueprintNotFoundException>(
            () => registry.GetOrActivateAsync("anything"));
    }

    [Fact(DisplayName = "Pre-existing IShellBlueprintProvider DI registration + AddShell throws at AddCShells return")]
    public void PreExistingProviderRegistration_PlusAddShell_ThrowsAtAddCShells()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        // Host pre-registered a provider directly — bypassing the AddBlueprintProvider seam.
        services.AddSingleton<IShellBlueprintProvider>(_ => new StubShellBlueprintProvider());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddCShells(c => c
                .WithAssemblies()
                .AddShell("would-be-lost", _ => { })));

        Assert.Contains("pre-existing IShellBlueprintProvider", ex.Message);
        Assert.Contains("silently have no effect", ex.Message);
        Assert.Contains("AddBlueprintProvider", ex.Message);
    }

    [Fact(DisplayName = "Pre-existing IShellBlueprintProvider DI registration + AddBlueprintProvider also throws")]
    public void PreExistingProviderRegistration_PlusAddBlueprintProvider_ThrowsAtAddCShells()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IShellBlueprintProvider>(_ => new StubShellBlueprintProvider());

        Assert.Throws<InvalidOperationException>(() =>
            services.AddCShells(c => c
                .WithAssemblies()
                .AddBlueprintProvider(_ => new StubShellBlueprintProvider().Add("a"))));
    }

    [Fact(DisplayName = "Pre-existing IShellBlueprintProvider DI registration alone (no builder state) is allowed (deliberate override)")]
    public async Task PreExistingProviderRegistration_Alone_IsAllowed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IShellBlueprintProvider>(_ => new StubShellBlueprintProvider().Add("custom"));

        // Should NOT throw — host took deliberate ownership of the binding without conflicting
        // builder state. The host's provider is what the registry will use.
        services.AddCShells(c => c.WithAssemblies());

        // The host's provider remains the resolved binding (TryAddSingleton skipped CShells's factory).
        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IShellRegistry>();
        Assert.NotNull(registry);

        var shell = await registry.GetOrActivateAsync("custom");
        Assert.Equal("custom", shell.Descriptor.Name);
    }

    [Fact(DisplayName = "Pre-existing IShellBlueprintProvider DI registration receives ConfigureAllShells defaults")]
    public async Task PreExistingProviderRegistration_WithConfigureAllShells_ComposesDefaults()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IShellBlueprintProvider>(_ => new StubShellBlueprintProvider().Add("custom"));

        services.AddCShells(c => c
            .WithAssemblies()
            .ConfigureAllShells(shell => shell
                .WithFeatures("CommonFeature")
                .WithConfiguration("Plan", "Standard")));

        await using var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<IShellBlueprintProvider>();
        var provided = await provider.GetAsync("custom");

        Assert.NotNull(provided);
        var settings = await provided.Blueprint.ComposeAsync();
        Assert.Contains("CommonFeature", settings.EnabledFeatures);
        Assert.Equal("Standard", settings.GetConfiguration("Plan"));
    }

    [Fact(DisplayName = "Repeated AddCShells calls add defaults once to a pre-existing blueprint provider")]
    public async Task PreExistingProviderRegistration_RepeatedConfigureAllShells_ComposesDefaultsOnce()
    {
        var firstDefaultCalls = 0;
        var secondDefaultCalls = 0;
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IShellBlueprintProvider>(_ =>
            new StubShellBlueprintProvider().Add("custom", shell => shell.WithConfiguration("Winner", "shell-specific")));

        services.AddCShells(c => c
            .WithAssemblies()
            .ConfigureAllShells(shell =>
            {
                firstDefaultCalls++;
                shell.WithConfiguration("FirstDefault", "first")
                    .WithConfiguration("Winner", "first-default");
            }));
        services.AddCShells(c => c.ConfigureAllShells(shell =>
        {
            secondDefaultCalls++;
            shell.WithConfiguration("SecondDefault", "second")
                .WithConfiguration("Winner", "second-default");
        }));

        await using var sp = services.BuildServiceProvider();
        var shell = await sp.GetRequiredService<IShellRegistry>().GetOrActivateAsync("custom");
        var settings = shell.ServiceProvider.GetRequiredService<ShellSettings>();

        Assert.Equal(1, firstDefaultCalls);
        Assert.Equal(1, secondDefaultCalls);
        Assert.Equal("first", settings.GetConfiguration("FirstDefault"));
        Assert.Equal("second", settings.GetConfiguration("SecondDefault"));
        Assert.Equal("shell-specific", settings.GetConfiguration("Winner"));
    }

    [Fact(DisplayName = "Third-party custom IShellBlueprintProvider activates shells identically to shipped providers (SC-008)")]
    public async Task ThirdPartyCustomProvider_RegisteredViaAddBlueprintProvider_ActivatesShellsLikeShippedProviders()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShells(c => c
            .WithAssemblies()
            .AddBlueprintProvider(_ => new ThirdPartyShellBlueprintProvider("custom-tenant")));

        await using var host = services.BuildServiceProvider();

        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.GetOrActivateAsync("custom-tenant");

        Assert.Equal("custom-tenant", shell.Descriptor.Name);
        Assert.Equal(1, shell.Descriptor.Generation);
        Assert.Equal(ShellLifecycleState.Active, shell.State);
    }

    /// <summary>
    /// A test-only third-party provider that is NOT shipped by the framework — proves that
    /// the IShellBlueprintProvider extension seam is open and any implementation works
    /// through the same AddBlueprintProvider registration path.
    /// </summary>
    private sealed class ThirdPartyShellBlueprintProvider(string ownedName) : IShellBlueprintProvider
    {
        public Task<ProvidedBlueprint?> GetAsync(string name, CancellationToken ct = default)
        {
            if (string.Equals(name, ownedName, StringComparison.OrdinalIgnoreCase))
            {
                var bp = new DelegateShellBlueprint(ownedName, _ => { });
                return Task.FromResult<ProvidedBlueprint?>(new ProvidedBlueprint(bp));
            }
            return Task.FromResult<ProvidedBlueprint?>(null);
        }

        public Task<BlueprintPage> ListAsync(BlueprintListQuery query, CancellationToken ct = default) =>
            Task.FromResult(new BlueprintPage(
                [new BlueprintSummary(ownedName, "ThirdParty", Mutable: false, new Dictionary<string, string>())],
                NextCursor: null));
    }
}
