using CShells.Configuration;
using CShells.Features;
using CShells.Hosting;
using CShells.Lifecycle;
using CShells.Lifecycle.Providers;  // for InMemoryShellBlueprintProvider
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CShells.DependencyInjection;

/// <summary>
/// ServiceCollection extensions for registering CShells.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers CShells services and returns a builder for further configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action to customize the CShells builder.</param>
    /// <returns>A CShells builder for further configuration.</returns>
    public static CShellsBuilder AddCShells(
        this IServiceCollection services,
        Action<CShellsBuilder>? configure = null)
    {
        Guard.Against.Null(services);

        // Snapshot whether the host has any pre-existing IShellBlueprintProvider registration.
        // We use TryAddSingleton below, which silently skips when a prior registration exists —
        // and silent skipping would mean any AddShell / AddBlueprintProvider builder calls
        // afterwards have no effect on the actual provider selected at runtime. Detect the
        // bypass at the END of this method (after `configure` runs) and throw with a teaching
        // message so the user is never left guessing why their blueprints "disappeared."
        var hadBlueprintProviderRegistrationBefore =
            services.Any(d => d.ServiceType == typeof(IShellBlueprintProvider));

        // Register the root service collection accessor so the provider builder can copy root
        // service registrations into each shell's service collection.
        services.TryAddSingleton<IRootServiceCollectionAccessor>(
            _ => new RootServiceCollectionAccessor(services));

        // Core infrastructure.
        services.AddSingleton<IShellServiceExclusionProvider, DefaultShellServiceExclusionProvider>();
        services.TryAddSingleton<IShellServiceExclusionRegistry, ShellServiceExclusionRegistry>();
        services.TryAddSingleton<IShellFeatureFactory, DefaultShellFeatureFactory>();

        var builder = new CShellsBuilder(services);

        services.TryAddSingleton<RuntimeFeatureCatalog>(sp =>
        {
            var logger = sp.GetService<ILogger<RuntimeFeatureCatalog>>();
            return new RuntimeFeatureCatalog(ct => builder.BuildFeatureAssembliesAsync(sp, ct), logger);
        });

        // Public, read-only view over the discovered feature catalog so hosts can present "available"
        // features (enabled or not) without re-implementing feature discovery. Excluded from per-shell
        // containers and root-delegated by ShellProviderBuilder so shell-scoped resolves alias the root.
        services.TryAddSingleton<IRuntimeFeatureCatalog>(sp => sp.GetRequiredService<RuntimeFeatureCatalog>());

        // Lifecycle surface: provider builder + registry + auto-logger + drain defaults + startup service.
        services.TryAddSingleton<ShellProviderBuilder>(sp => new ShellProviderBuilder(
            sp.GetRequiredService<IRootServiceCollectionAccessor>(),
            sp,
            sp.GetRequiredService<IShellServiceExclusionRegistry>(),
            sp.GetRequiredService<IShellFeatureFactory>(),
            sp.GetRequiredService<RuntimeFeatureCatalog>(),
            sp.GetService<ILogger<ShellProviderBuilder>>()));

        // Blueprint provider: exactly one is registered. Default is the built-in in-memory
        // provider populated from AddShell(...) calls. Hosts that register an external provider
        // via AddBlueprintProvider(...) replace the default. Mixing the two raises a teaching
        // exception at the moment IShellBlueprintProvider is first resolved (which is during
        // CShellsStartupHostedService.StartAsync, well before any HTTP traffic flows).
        services.TryAddSingleton<IShellBlueprintProvider>(sp =>
        {
            var hasInline = builder.InlineBlueprints.Count > 0;
            var externalCount = builder.ProviderFactories.Count;

            if (externalCount > 1)
            {
                throw new InvalidOperationException(
                    "CShells permits exactly one external IShellBlueprintProvider per host, but " +
                    $"{externalCount} were registered via AddBlueprintProvider (or its sugar " +
                    "extensions WithConfigurationProvider, WithFluentStorageBlueprints, ...). " +
                    "Pick one source. If you genuinely need to combine multiple sources, " +
                    "implement a custom IShellBlueprintProvider that fans out to your sub-sources " +
                    "internally and register only that single provider.");
            }

            if (hasInline && externalCount == 1)
            {
                throw new InvalidOperationException(
                    "CShells permits exactly one blueprint provider per host, but this host " +
                    "registered both: AddShell(...) registers blueprints with the in-memory " +
                    "provider, and AddBlueprintProvider(...) (or its sugar — " +
                    "WithConfigurationProvider, WithFluentStorageBlueprints, etc.) registers an " +
                    "external provider. Resolve the conflict in one of three ways: " +
                    "(1) move the AddShell blueprints into the external source; " +
                    "(2) drop the external provider and keep AddShell; " +
                    "(3) implement a custom IShellBlueprintProvider that combines both sources " +
                    "and register only that single provider.");
            }

            var provider = externalCount == 1
                ? builder.ProviderFactories[0](sp)
                : new InMemoryShellBlueprintProvider(builder.InlineBlueprints);

            return builder.ShellConfigurators.Count == 0
                ? provider
                : new ConfiguredShellBlueprintProvider(provider, builder.ShellConfigurators);
        });

        services.TryAddSingleton<ShellRegistry>(sp => new ShellRegistry(
            sp.GetRequiredService<IShellBlueprintProvider>(),
            sp.GetRequiredService<ShellProviderBuilder>(),
            sp,
            sp.GetService<ILogger<ShellRegistry>>(),
            sp.GetServices<IShellLifecycleSubscriber>(),
            sp.GetServices<IShellGenerationActivationParticipant>()));
        services.TryAddSingleton<IShellRegistry>(sp => sp.GetRequiredService<ShellRegistry>());

        services.TryAddSingleton<IDrainPolicy>(_ => new Lifecycle.Policies.FixedTimeoutDrainPolicy(TimeSpan.FromSeconds(30)));
        services.TryAddSingleton(DrainGracePeriod.Default);

        services.AddSingleton<ShellLifecycleLogger>();
        services.AddSingleton<IShellLifecycleSubscriber>(sp => sp.GetRequiredService<ShellLifecycleLogger>());

        services.AddHostedService<CShellsStartupHostedService>();

        configure?.Invoke(builder);

        // Bypass guard: if the host pre-registered IShellBlueprintProvider AND also added
        // builder-side blueprints (via AddShell or AddBlueprintProvider), the TryAddSingleton
        // above silently skipped — so the builder state would have no effect at runtime. This
        // is the same class of failure FR-005 / FR-006 catch at the factory; the difference is
        // that this case never reaches the factory at all because it was bypassed entirely.
        // (Hosts that override IShellBlueprintProvider AFTER AddCShells are making a deliberate
        // replacement and are NOT caught here — that's an advanced extension point we permit.)
        if (hadBlueprintProviderRegistrationBefore &&
            (builder.InlineBlueprints.Count > 0 || builder.ProviderFactories.Count > 0))
        {
            throw new InvalidOperationException(
                "CShells detected a pre-existing IShellBlueprintProvider DI registration alongside " +
                "AddShell or AddBlueprintProvider builder calls on the same host. The builder's " +
                "blueprints would silently have no effect because the pre-existing registration " +
                "takes precedence. Resolve this by either: " +
                "(1) removing the manual IShellBlueprintProvider registration and using " +
                "AddBlueprintProvider(...) instead so CShells's fail-fast guard can govern it; or " +
                "(2) removing the AddShell / AddBlueprintProvider builder calls if you intend to " +
                "manage IShellBlueprintProvider yourself outside the CShells builder.");
        }

        if (hadBlueprintProviderRegistrationBefore && builder.ShellConfigurators.Count > 0)
            DecoratePreExistingBlueprintProvider(services, builder.ShellConfigurators);

        return builder;
    }

    private static void DecoratePreExistingBlueprintProvider(
        IServiceCollection services,
        IReadOnlyList<Action<ShellBuilder>> shellConfigurators)
    {
        var index = FindLastBlueprintProviderRegistration(services);
        if (index < 0)
            return;

        var descriptor = services[index];
        services.RemoveAt(index);
        services.Add(ServiceDescriptor.Describe(
            typeof(IShellBlueprintProvider),
            sp => new ConfiguredShellBlueprintProvider(CreateBlueprintProvider(sp, descriptor), shellConfigurators),
            descriptor.Lifetime));
    }

    private static int FindLastBlueprintProviderRegistration(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
            if (services[i].ServiceType == typeof(IShellBlueprintProvider))
                return i;

        return -1;
    }

    private static IShellBlueprintProvider CreateBlueprintProvider(IServiceProvider services, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IShellBlueprintProvider instance)
            return instance;

        if (descriptor.ImplementationFactory is not null)
            return (IShellBlueprintProvider)descriptor.ImplementationFactory(services);

        if (descriptor.ImplementationType is not null)
            return (IShellBlueprintProvider)ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType);

        throw new InvalidOperationException("Unsupported IShellBlueprintProvider registration.");
    }
}
