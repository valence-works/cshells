using System.Reflection;
using CShells.Configuration;
using CShells.Features;
using CShells.Lifecycle;
using CShells.Lifecycle.Blueprints;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.DependencyInjection;

/// <summary>
/// Builder for configuring CShells services with a fluent API. Each registered shell becomes
/// an <see cref="IShellBlueprint"/> that the <see cref="IShellRegistry"/> resolves at activation
/// time.
/// </summary>
public class CShellsBuilder
{
    private readonly List<Func<IServiceProvider, IFeatureAssemblyProvider>> _featureAssemblyProviderRegistrations = [];
    private readonly List<ISharedAssemblySelector> _sharedAssemblySelectors = [];
    private readonly List<Action<ShellBuilder>> _shellConfigurators = new();
    private readonly List<IShellBlueprint> _inlineBlueprints = [];
    private readonly List<Func<IServiceProvider, IShellBlueprintProvider>> _providerFactories = [];
    private readonly List<Type> _sharedSingletonServiceTypes = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CShellsBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public CShellsBuilder(IServiceCollection services)
    {
        Services = Guard.Against.Null(services);
    }

    /// <summary>Blueprints contributed via <see cref="AddShell"/> and <see cref="AddBlueprint"/>, in registration order.</summary>
    internal IReadOnlyList<IShellBlueprint> InlineBlueprints => _inlineBlueprints;

    /// <summary>Factories that resolve additional <see cref="IShellBlueprintProvider"/> instances at DI-resolution time.</summary>
    internal IReadOnlyList<Func<IServiceProvider, IShellBlueprintProvider>> ProviderFactories => _providerFactories;

    internal IReadOnlyList<Type> SharedSingletonServiceTypes => _sharedSingletonServiceTypes;

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets the configurators registered via <see cref="ConfigureAllShells"/>.
    /// </summary>
    internal IReadOnlyList<Action<ShellBuilder>> ShellConfigurators => _shellConfigurators.AsReadOnly();

    /// <summary>
    /// Gets a value indicating whether explicit feature assembly providers were configured.
    /// </summary>
    internal bool UsesExplicitFeatureAssemblyProviders => _featureAssemblyProviderRegistrations.Count > 0;

    internal IReadOnlyList<ISharedAssemblySelector> SharedAssemblySelectors => _sharedAssemblySelectors.AsReadOnly();

    internal IReadOnlyList<SharedAssemblyMatch> SharedAssemblyMatches { get; private set; } = [];

    /// <summary>
    /// Registers a feature assembly provider factory.
    /// </summary>
    internal void RegisterFeatureAssemblyProvider(Func<IServiceProvider, IFeatureAssemblyProvider> registration)
    {
        _featureAssemblyProviderRegistrations.Add(Guard.Against.Null(registration));
    }

    internal void AddSharedAssemblySelector(ISharedAssemblySelector selector)
    {
        _sharedAssemblySelectors.Add(Guard.Against.Null(selector));
    }

    /// <summary>
    /// Builds all registered feature assembly providers and returns them in registration order.
    /// </summary>
    internal IReadOnlyList<IFeatureAssemblyProvider> BuildFeatureAssemblyProviders(IServiceProvider serviceProvider)
    {
        Guard.Against.Null(serviceProvider);

        var providers = new List<IFeatureAssemblyProvider>(_featureAssemblyProviderRegistrations.Count);

        foreach (var registration in _featureAssemblyProviderRegistrations)
        {
            var provider = registration(serviceProvider)
                ?? throw new InvalidOperationException("Feature assembly provider registrations must return a non-null provider instance.");

            providers.Add(provider);
        }

        return providers.AsReadOnly();
    }

    /// <summary>
    /// Resolves the assemblies that should be scanned for shell feature discovery.
    /// </summary>
    internal async Task<IReadOnlyCollection<Assembly>> BuildFeatureAssembliesAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(serviceProvider);

        var selectorProvider = new SharedAssemblySelectorProvider(_sharedAssemblySelectors);
        var resolvedAssemblies = UsesExplicitFeatureAssemblyProviders
            ? [.. await CShells.Features.FeatureAssemblyResolver.ResolveAssembliesAsync(BuildFeatureAssemblyProviders(serviceProvider), serviceProvider, cancellationToken)]
            : new List<Assembly>();

        if (selectorProvider.HasSelectors)
            resolvedAssemblies.AddRange(CShells.Features.FeatureAssemblyResolver.ResolveHostAssemblies(selectorProvider.IsMatch));
        else if (!UsesExplicitFeatureAssemblyProviders)
            resolvedAssemblies.AddRange(CShells.Features.FeatureAssemblyResolver.ResolveHostAssemblies());

        SharedAssemblyMatches = selectorProvider.Matches;

        return CShells.Features.FeatureAssemblyResolver.DeduplicateAssemblies(resolvedAssemblies);
    }

    /// <summary>
    /// Registers a configurator whose settings are used as defaults whenever shell blueprints
    /// are composed. Shell-specific blueprint settings are merged afterwards and take
    /// precedence for conflicting configuration keys.
    /// </summary>
    public CShellsBuilder ConfigureAllShells(Action<ShellBuilder> configure)
    {
        Guard.Against.Null(configure);
        _shellConfigurators.Add(configure);
        return this;
    }

    /// <summary>
    /// Shares every unkeyed root singleton registration for <typeparamref name="TService"/> with shell providers.
    /// </summary>
    /// <typeparam name="TService">The closed service type to share.</typeparam>
    /// <remarks>
    /// The root provider retains ownership of instances it creates. Shell providers receive the resolved instances
    /// without acquiring disposal ownership. Aliases must be selected separately.
    /// </remarks>
    /// <returns>This builder.</returns>
    public CShellsBuilder ShareSingletonWithShells<TService>() => ShareSingletonWithShells(typeof(TService));

    /// <summary>
    /// Shares every unkeyed root singleton registration for <paramref name="serviceType"/> with shell providers.
    /// </summary>
    /// <param name="serviceType">The closed service type whose unkeyed singleton registrations should be shared.</param>
    /// <remarks>
    /// Validation is performed when a shell provider is built, against the final root registrations and exclusion set.
    /// Keyed registrations and aliases are independent. The root provider retains ownership of instances it creates.
    /// </remarks>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <see langword="null"/>.</exception>
    public CShellsBuilder ShareSingletonWithShells(Type serviceType)
    {
        Guard.Against.Null(serviceType);
        if (!_sharedSingletonServiceTypes.Contains(serviceType))
            _sharedSingletonServiceTypes.Add(serviceType);

        return this;
    }

    /// <summary>
    /// Adds a shell blueprint with the given name. The supplied delegate runs against a fresh
    /// <see cref="ShellBuilder"/> whenever the blueprint is composed. Settings from
    /// <see cref="ConfigureAllShells"/> are merged in as defaults, so values
    /// configured here take precedence for conflicting configuration keys.
    /// </summary>
    /// <remarks>
    /// Blueprints added here are vended by the built-in <c>InMemoryShellBlueprintProvider</c>
    /// at DI-resolution time. Activation is lazy — the first request for the shell triggers
    /// construction.
    /// </remarks>
    public CShellsBuilder AddShell(string name, Action<ShellBuilder> configure)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.Null(configure);

        // ConfigureAllShells configurators are applied centrally by ConfiguredShellBlueprintProvider
        // during ComposeAsync, so only the shell-specific delegate is passed to the blueprint here.
        _inlineBlueprints.Add(new DelegateShellBlueprint(name, configure));
        return this;
    }

    /// <summary>
    /// Adds a blueprint directly to the in-memory provider.
    /// </summary>
    public CShellsBuilder AddBlueprint(IShellBlueprint blueprint)
    {
        _inlineBlueprints.Add(Guard.Against.Null(blueprint));
        return this;
    }

    /// <summary>
    /// Registers the host's external <see cref="IShellBlueprintProvider"/>, resolved from DI
    /// when the registry first needs it. Exactly one external provider is permitted per host;
    /// calling this method more than once — or calling it alongside <see cref="AddShell"/> —
    /// raises <see cref="InvalidOperationException"/> at startup with a teaching message that
    /// names the conflict and enumerates the valid resolutions.
    /// </summary>
    public CShellsBuilder AddBlueprintProvider(Func<IServiceProvider, IShellBlueprintProvider> factory)
    {
        _providerFactories.Add(Guard.Against.Null(factory));
        return this;
    }

}
