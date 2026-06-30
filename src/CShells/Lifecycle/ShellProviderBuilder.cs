using CShells.Configuration;
using CShells.DependencyInjection;
using CShells.Features;
using CShells.Features.Validation;
using CShells.Hosting;
using CShells.Lifecycle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Lifecycle;

/// <summary>
/// Builds a shell generation's <see cref="IServiceProvider"/> from its <see cref="ShellSettings"/>.
/// </summary>
/// <remarks>
/// The build pipeline: copy root services (minus infrastructure exclusions), register shell-scoped
/// core services (<see cref="ShellSettings"/>, <see cref="ShellId"/>,
/// <see cref="ShellConfiguration"/>, <see cref="IConfiguration"/>, feature descriptors,
/// <see cref="IShell"/>), invoke each enabled feature's <see cref="IShellFeature.ConfigureServices"/>
/// in dependency order, then build. Features that need "the shell I belong to" inject
/// <see cref="IShell"/> directly.
/// </remarks>
internal sealed class ShellProviderBuilder(
    IRootServiceCollectionAccessor rootServicesAccessor,
    IServiceProvider rootProvider,
    IShellServiceExclusionRegistry exclusionRegistry,
    IShellFeatureFactory featureFactory,
    RuntimeFeatureCatalog featureCatalog,
    ILogger<ShellProviderBuilder>? logger = null)
{
    private readonly IRootServiceCollectionAccessor _rootServicesAccessor = Guard.Against.Null(rootServicesAccessor);
    private readonly IServiceProvider _rootProvider = Guard.Against.Null(rootProvider);
    private readonly IShellServiceExclusionRegistry _exclusionRegistry = Guard.Against.Null(exclusionRegistry);
    private readonly IShellFeatureFactory _featureFactory = Guard.Against.Null(featureFactory);
    private readonly RuntimeFeatureCatalog _featureCatalog = Guard.Against.Null(featureCatalog);
    private readonly ILogger<ShellProviderBuilder> _logger = logger ?? NullLogger<ShellProviderBuilder>.Instance;
    private readonly FeatureDependencyResolver _dependencyResolver = new();

    /// <summary>
    /// Builds a service provider for a shell composed from <paramref name="settings"/>.
    /// </summary>
    /// <returns>The provider, the holder that will be populated with the <see cref="IShell"/> reference, and the ordered enabled-feature list.</returns>
    public async Task<BuildResult> BuildAsync(ShellSettings settings, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(settings);

        await _featureCatalog.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var catalog = _featureCatalog.CurrentSnapshot;

        // Explicit disabled entries are already excluded from EnabledFeatures. Unknown positive entries
        // are tolerated so hosts can share configuration across deployments that do not reference
        // every optional feature package.
        var missingFeatures = settings.EnabledFeatures
            .Where(id => !catalog.FeatureMap.ContainsKey(id))
            .ToList();

        if (missingFeatures.Count > 0)
        {
            _logger.LogWarning(
                "Shell '{ShellName}' requested {MissingFeatureCount} feature(s) that are not available in the runtime feature catalog: {MissingFeatures}. The shell will activate with available features only.",
                settings.Id.Name,
                missingFeatures.Count,
                missingFeatures);
        }

        var availableFeatures = settings.EnabledFeatures
            .Where(id => catalog.FeatureMap.ContainsKey(id))
            .ToList();

        var orderedFeatures = availableFeatures.Count > 0
            ? _dependencyResolver.GetOrderedFeatures(availableFeatures, catalog.FeatureMap)
            : [];

        // Dependencies are effective shell features too.
        settings.EnabledFeatures = [..orderedFeatures];

        var services = new ServiceCollection();
        CopyRootServices(services);

        var holder = new ShellHolder();
        RegisterCoreServices(services, settings, holder, catalog.FeatureDescriptors);

        ConfigureFeatures(services, settings, orderedFeatures, catalog.FeatureMap);

        var provider = services.BuildServiceProvider();

        return new BuildResult(provider, holder, orderedFeatures.AsReadOnly());
    }

    private void CopyRootServices(IServiceCollection shellServices)
    {
        var excluded = _exclusionRegistry.ExcludedTypes;
        foreach (var descriptor in _rootServicesAccessor.Services)
        {
            if (excluded.Contains(descriptor.ServiceType))
                continue;
            shellServices.Add(descriptor);
        }
    }

    private void RegisterCoreServices(
        IServiceCollection services,
        ShellSettings settings,
        ShellHolder holder,
        IReadOnlyCollection<ShellFeatureDescriptor> featureDescriptors)
    {
        services.AddSingleton(settings);
        services.Add(ServiceDescriptor.Singleton(typeof(ShellId), settings.Id));

        services.AddLogging();

        // Shell-merged configuration + IConfiguration override.
        services.AddSingleton(_ =>
        {
            var rootConfiguration = _rootProvider.GetRequiredService<IConfiguration>();
            return new ShellConfiguration(settings, rootConfiguration);
        });
        services.AddSingleton<IConfiguration>(sp => sp.GetRequiredService<ShellConfiguration>());

        // Feature descriptors.
        var descriptorList = featureDescriptors.ToList().AsReadOnly();
        services.AddSingleton<IReadOnlyCollection<ShellFeatureDescriptor>>(descriptorList);
        services.AddSingleton<IEnumerable<ShellFeatureDescriptor>>(descriptorList);

        // IShell: registered via the holder that the registry populates after construction.
        services.AddSingleton(holder);
        services.AddSingleton<IShell>(sp => sp.GetRequiredService<ShellHolder>().Shell);

        // Root-delegation for IShellRegistry: shells legitimately need registry access (for
        // reload triggers, enumerating sibling shells, etc.), but copying the root factory
        // would cascade through excluded root-only infrastructure. Delegate back to the root
        // singleton so shell-scoped resolves alias cleanly.
        var rootProvider = _rootProvider;
        services.AddSingleton<IShellRegistry>(_ => rootProvider.GetRequiredService<IShellRegistry>());

        // Root-delegation for IRuntimeFeatureCatalog: shell-scoped consumers (e.g. a feature-management UI
        // feature) may read the discovered catalog, but the catalog is root-only infrastructure. Alias to the
        // root singleton rather than copying the excluded root factory.
        services.AddSingleton<IRuntimeFeatureCatalog>(_ => rootProvider.GetRequiredService<IRuntimeFeatureCatalog>());
    }

    private void ConfigureFeatures(
        IServiceCollection services,
        ShellSettings settings,
        IReadOnlyList<string> orderedFeatures,
        IReadOnlyDictionary<string, ShellFeatureDescriptor> featureMap)
    {
        if (orderedFeatures.Count == 0)
            return;

        var featureContext = new ShellFeatureContext(settings, featureMap.Values.ToList());
        var pendingPostConfigure = new List<IPostConfigureShellServices>();

        foreach (var featureName in orderedFeatures)
        {
            var descriptor = featureMap[featureName];
            if (descriptor.StartupType is null)
                continue;

            try
            {
                var feature = _featureFactory.CreateFeature<IShellFeature>(descriptor.StartupType, settings, featureContext);

                ApplyConfiguration(feature, settings, featureName);

                if (settings.FeatureConfigurators.TryGetValue(featureName, out var configurator))
                    configurator(feature);

                feature.ConfigureServices(services);

                if (feature is IPostConfigureShellServices postConfigure)
                    pendingPostConfigure.Add(postConfigure);

                _logger.LogDebug("Configured services from feature '{FeatureName}' ({StartupType})", featureName, descriptor.StartupType.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure services for feature '{FeatureName}'", featureName);
                throw new InvalidOperationException(
                    $"Failed to configure services for feature '{featureName}': {ex.Message}", ex);
            }
        }

        // PostConfigure runs after every feature's ConfigureServices so late-bound registrations see the full picture.
        foreach (var postConfigure in pendingPostConfigure)
            postConfigure.PostConfigureServices(services);
    }

    private void ApplyConfiguration(IShellFeature feature, ShellSettings settings, string featureName)
    {
        try
        {
            var rootConfiguration = _rootProvider.GetRequiredService<IConfiguration>();
            var shellConfiguration = new ShellConfiguration(settings, rootConfiguration);
            var validator = _rootProvider.GetService<IFeatureConfigurationValidator>()
                            ?? new DataAnnotationsFeatureConfigurationValidator();
            var binder = new FeatureConfigurationBinder(
                shellConfiguration,
                validator,
                _rootProvider.GetService<ILogger<FeatureConfigurationBinder>>());
            binder.BindAndConfigure(feature, featureName);
        }
        catch (FeatureConfigurationValidationException)
        {
            throw; // Surface validation failures.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply configuration to feature '{FeatureName}'. Feature will use defaults.", featureName);
        }
    }

    /// <summary>Output of <see cref="BuildAsync"/>.</summary>
    public sealed record BuildResult(
        ServiceProvider Provider,
        ShellHolder Holder,
        IReadOnlyList<string> EnabledFeatures);
}
