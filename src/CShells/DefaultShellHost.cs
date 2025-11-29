using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells;

/// <summary>
/// Default implementation of <see cref="IShellHost"/> that builds and caches per-shell
/// <see cref="IServiceProvider"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// Shell features (<see cref="IShellFeature"/> implementations) are instantiated using the
/// application's root <see cref="IServiceProvider"/> via <see cref="ActivatorUtilities.CreateInstance"/>,
/// with <see cref="ShellSettings"/> passed as an explicit parameter. This means:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>Feature constructors may only depend on root-level services (logging, configuration, etc.) and ShellSettings.</description>
///   </item>
///   <item>
///     <description>No temporary shell ServiceProviders are created during feature configuration.</description>
///   </item>
///   <item>
///     <description>Shell services are registered purely via IServiceCollection in ConfigureServices.</description>
///   </item>
/// </list>
/// </remarks>
public class DefaultShellHost : IShellHost, IDisposable
{
    private readonly IReadOnlyDictionary<string, ShellFeatureDescriptor> _featureMap;
    private readonly IReadOnlyList<ShellSettings> _shellSettings;
    private readonly IServiceProvider _rootProvider;
    private readonly IServiceCollection _rootServices;
    private readonly ConcurrentDictionary<ShellId, ShellContext> _shellContexts = new();
    private readonly FeatureDependencyResolver _dependencyResolver = new();
    private readonly ILogger<DefaultShellHost> _logger;
    private readonly Lock _buildLock = new();
    private bool _disposed;

    // Cached copy of root service descriptors for efficient bulk-copy to shell service collections.
    // This avoids re-enumerating the root IServiceCollection for each shell.
    private List<ServiceDescriptor>? _cachedRootDescriptors;

    /// <summary>
    /// CShell infrastructure service types that should NOT be copied into shell containers.
    /// Copying these would cause shells to resolve a new DefaultShellHost using the shell provider
    /// as the "root," breaking the documented semantics and fragmenting the shell cache.
    /// </summary>
    private static readonly HashSet<Type> ExcludedInfrastructureTypes =
    [
        typeof(IShellHost),
        typeof(IShellContextScopeFactory),
        typeof(IRootServiceCollectionAccessor),
        typeof(IReadOnlyCollection<ShellSettings>)
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShellHost"/> class.
    /// </summary>
    /// <param name="shellSettings">The collection of shell settings to manage.</param>
    /// <param name="rootProvider">
    /// The application's root <see cref="IServiceProvider"/> used to instantiate <see cref="IShellFeature"/> implementations.
    /// Feature constructors can resolve root-level services (logging, configuration, etc.).
    /// </param>
    /// <param name="rootServicesAccessor">
    /// An accessor to the root <see cref="IServiceCollection"/>. Root service registrations
    /// are copied into each shell's service collection, enabling inheritance of root services.
    /// </param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shellSettings"/>, <paramref name="rootProvider"/>, or <paramref name="rootServicesAccessor"/> is null.</exception>
    public DefaultShellHost(
        IEnumerable<ShellSettings> shellSettings,
        IServiceProvider rootProvider,
        IRootServiceCollectionAccessor rootServicesAccessor,
        ILogger<DefaultShellHost>? logger = null)
        : this(shellSettings, AppDomain.CurrentDomain.GetAssemblies(), rootProvider, rootServicesAccessor, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShellHost"/> class with custom assemblies and root service collection.
    /// </summary>
    /// <param name="shellSettings">The collection of shell settings to manage.</param>
    /// <param name="assemblies">The assemblies to scan for features.</param>
    /// <param name="rootProvider">
    /// The application's root <see cref="IServiceProvider"/> used to instantiate <see cref="IShellFeature"/> implementations.
    /// Feature constructors can resolve root-level services (logging, configuration, etc.).
    /// </param>
    /// <param name="rootServicesAccessor">
    /// An accessor to the root <see cref="IServiceCollection"/>. Root service registrations
    /// are copied into each shell's service collection, enabling inheritance of root services.
    /// </param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shellSettings"/>, <paramref name="assemblies"/>, <paramref name="rootProvider"/>, or <paramref name="rootServicesAccessor"/> is null.</exception>
    public DefaultShellHost(
        IEnumerable<ShellSettings> shellSettings,
        IEnumerable<Assembly> assemblies,
        IServiceProvider rootProvider,
        IRootServiceCollectionAccessor rootServicesAccessor,
        ILogger<DefaultShellHost>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(shellSettings);
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(rootProvider);
        ArgumentNullException.ThrowIfNull(rootServicesAccessor);
        
        _shellSettings = shellSettings.ToList();
        _rootProvider = rootProvider;
        _rootServices = rootServicesAccessor.Services;
        _logger = logger ?? NullLogger<DefaultShellHost>.Instance;

        // Discover all features from specified assemblies
        var features = FeatureDiscovery.DiscoverFeatures(assemblies).ToList();

        _logger.LogInformation("Discovered {FeatureCount} features: {FeatureNames}",
            features.Count,
            string.Join(", ", features.Select(f => f.Id)));

        // Build feature map for quick lookup
        _featureMap = features.ToDictionary(f => f.Id, f => f, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public ShellContext DefaultShell
    {
        get
        {
            ThrowIfDisposed();

            // Try to find shell with Id "Default"
            var defaultId = new ShellId("Default");
            if (_shellContexts.TryGetValue(defaultId, out var context))
            {
                return context;
            }

            // Check if there's a settings entry for "Default"
            var defaultSettings = _shellSettings.FirstOrDefault(s =>
                string.Equals(s.Id.Name, "Default", StringComparison.OrdinalIgnoreCase));

            if (defaultSettings != null)
            {
                return GetShell(defaultSettings.Id);
            }

            // Otherwise, return the first shell
            var firstSettings = _shellSettings.FirstOrDefault();
            if (firstSettings == null)
            {
                throw new InvalidOperationException("No shells have been configured.");
            }

            return GetShell(firstSettings.Id);
        }
    }

    /// <inheritdoc />
    public ShellContext GetShell(ShellId id)
    {
        ThrowIfDisposed();

        // Try to get from cache first
        if (_shellContexts.TryGetValue(id, out var context))
        {
            return context;
        }

        // Build the shell context
        return BuildShellContext(id);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ShellContext> AllShells
    {
        get
        {
            ThrowIfDisposed();

            // Build all shells that haven't been built yet
            foreach (var settings in _shellSettings)
            {
                // Use GetOrAdd pattern to build each shell only once
                _shellContexts.GetOrAdd(settings.Id, _ => BuildShellContextInternal(settings));
            }

            return _shellContexts.Values.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Builds a shell context for the specified shell ID.
    /// </summary>
    private ShellContext BuildShellContext(ShellId id)
    {
        // Find the settings for this shell
        var settings = _shellSettings.FirstOrDefault(s => s.Id.Equals(id));
        if (settings == null)
        {
            throw new KeyNotFoundException($"Shell with Id '{id}' was not found in the configured shell settings.");
        }

        return _shellContexts.GetOrAdd(id, _ => BuildShellContextInternal(settings));
    }

    /// <summary>
    /// Internal method to build a shell context for the given settings.
    /// This method is called within GetOrAdd and ensures thread-safe initialization.
    /// </summary>
    private ShellContext BuildShellContextInternal(ShellSettings settings)
    {
        // Double-check locking for thread safety during initialization
        lock (_buildLock)
        {
            // Check again after acquiring lock in case another thread already built it
            if (_shellContexts.TryGetValue(settings.Id, out var existingContext))
            {
                return existingContext;
            }

            _logger.LogInformation("Building shell context for '{ShellId}'", settings.Id);

            ValidateEnabledFeatures(settings);
            var orderedFeatures = ResolveFeatureDependencies(settings);

            _logger.LogInformation("Shell '{ShellId}' will use features (in order): {Features}",
                settings.Id, string.Join(", ", orderedFeatures));

            return CreateShellContext(settings, orderedFeatures);
        }
    }

    /// <summary>
    /// Resolves feature dependencies and returns an ordered list of features for the shell.
    /// </summary>
    private List<string> ResolveFeatureDependencies(ShellSettings settings)
    {
        try
        {
            return _dependencyResolver.GetOrderedFeatures(settings.EnabledFeatures, _featureMap);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to resolve feature dependencies for shell '{ShellId}'", settings.Id);
            throw;
        }
    }

    /// <summary>
    /// Creates a shell context with its service provider and configured features.
    /// Uses the holder pattern to allow ShellContext to be resolved from DI.
    /// </summary>
    private ShellContext CreateShellContext(ShellSettings settings, List<string> orderedFeatures)
    {
        var contextHolder = new ShellContextHolder();
        var serviceProvider = BuildServiceProvider(settings, orderedFeatures, contextHolder);
        var context = new ShellContext(settings, serviceProvider);

        // Populate the holder so ShellContext can be resolved from DI
        contextHolder.Context = context;

        return context;
    }

    /// <summary>
    /// Validates that all enabled features in the shell settings are known/discovered features.
    /// </summary>
    /// <param name="settings">The shell settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown when an unknown feature is configured.</exception>
    private void ValidateEnabledFeatures(ShellSettings settings)
    {
        var unknownFeatures = settings.EnabledFeatures
            .Where(featureName => !_featureMap.ContainsKey(featureName))
            .ToList();

        foreach (var featureName in unknownFeatures)
        {
            _logger.LogWarning("Unknown feature '{FeatureName}' configured for shell '{ShellId}'",
                featureName, settings.Id);
        }

        if (unknownFeatures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Feature(s) '{string.Join(", ", unknownFeatures)}' configured for shell '{settings.Id}' were not found in discovered features.");
        }
    }

    /// <summary>
    /// Builds an <see cref="IServiceProvider"/> for a shell with the specified features.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    /// <param name="orderedFeatures">The ordered list of features to configure.</param>
    /// <param name="contextHolder">A holder that will be populated with the ShellContext after the service provider is built.</param>
    /// <remarks>
    /// <para>
    /// The service provider is built by:
    /// </para>
    /// <list type="number">
    ///   <item><description>Creating a fresh <see cref="ServiceCollection"/> for the shell.</description></item>
    ///   <item><description>Copying all <see cref="ServiceDescriptor"/> entries from the root <see cref="IServiceCollection"/>.</description></item>
    ///   <item><description>Adding shell-specific core services (ShellSettings, ShellId, ShellContext).</description></item>
    ///   <item><description>Invoking <see cref="IShellFeature.ConfigureServices"/> for each enabled feature in dependency order.</description></item>
    ///   <item><description>Building the <see cref="IServiceProvider"/> only after all registrations are complete.</description></item>
    /// </list>
    /// <para>
    /// Because root services are added first and shell-specific services are added after,
    /// the DI container's "last registration wins" semantics ensure that shell-specific
    /// registrations override root registrations for the same service type.
    /// </para>
    /// </remarks>
    private IServiceProvider BuildServiceProvider(ShellSettings settings, List<string> orderedFeatures, ShellContextHolder contextHolder)
    {
        var shellServices = new ServiceCollection();

        // Step 1: Copy all root service registrations to the shell's service collection.
        // This enables inheritance of root services in shells.
        CopyRootServices(shellServices);

        // Step 2: Register shell-specific core services (ShellSettings, ShellId, ShellContext).
        // These are added after root services, so they override any root registrations.
        RegisterCoreServices(shellServices, settings, contextHolder);

        // Step 3: Configure feature services in dependency order.
        // Features can override root services by registering the same service type.
        if (orderedFeatures.Count > 0)
        {
            ConfigureFeatureServices(shellServices, orderedFeatures, settings);
        }

        // Step 4: Build the service provider only after all registrations are complete.
        // No temporary providers are created during feature configuration.
        return shellServices.BuildServiceProvider();
    }

    /// <summary>
    /// Copies all service descriptors from the root <see cref="IServiceCollection"/> to the shell's service collection,
    /// excluding CShell infrastructure types that should not be inherited by shells.
    /// </summary>
    /// <param name="shellServices">The shell's service collection to copy to.</param>
    /// <remarks>
    /// <para>
    /// This enables inheritance of root services in shells. Because these registrations are added first,
    /// shell-specific registrations added later will override them via "last registration wins" semantics.
    /// </para>
    /// <para>
    /// CShell infrastructure types (IShellHost, IShellContextScopeFactory, etc.) are excluded from copying
    /// to prevent shells from resolving a new DefaultShellHost using the shell provider as the "root,"
    /// which would break the documented semantics and fragment the shell cache.
    /// </para>
    /// <para>
    /// Performance optimization: The filtered root service descriptors are cached on first access to avoid
    /// re-enumerating and filtering the root IServiceCollection for each shell.
    /// </para>
    /// <para>
    /// Thread safety: This method is called within BuildServiceProvider, which is invoked inside
    /// the _buildLock in BuildShellContextInternal, so the caching is thread-safe.
    /// </para>
    /// </remarks>
    private void CopyRootServices(ServiceCollection shellServices)
    {
        // Cache the filtered root descriptors on first access for efficient bulk-copy to subsequent shells.
        // This avoids repeatedly enumerating and filtering the root IServiceCollection.
        // Thread-safe: This method is always called under _buildLock (see BuildShellContextInternal).
        _cachedRootDescriptors ??= _rootServices
            .Where(d => !ExcludedInfrastructureTypes.Contains(d.ServiceType))
            .ToList();

        // Bulk-copy cached descriptors to the shell's service collection
        foreach (var descriptor in _cachedRootDescriptors)
        {
            shellServices.Add(descriptor);
        }

        _logger.LogDebug("Copied {Count} service descriptors from root service collection (excluded {ExcludedCount} infrastructure types)",
            _cachedRootDescriptors.Count,
            ExcludedInfrastructureTypes.Count);
    }

    /// <summary>
    /// Registers core services required by all shells.
    /// </summary>
    private static void RegisterCoreServices(ServiceCollection services, ShellSettings settings, ShellContextHolder contextHolder)
    {
        // Register shell settings and shell ID for convenience
        services.AddSingleton(settings);
        // ShellId is a value type, so we register it directly as a singleton instance
        // rather than through AddSingleton<T>() which requires a reference type
        services.Add(ServiceDescriptor.Singleton(typeof(ShellId), settings.Id));

        // Add logging services so shell containers work with ASP.NET Core infrastructure
        services.AddLogging();

        // Register the ShellContext using the holder pattern
        // The holder will be populated after the service provider is built
        services.AddSingleton<ShellContext>(sp => contextHolder.Context
            ?? throw new InvalidOperationException($"ShellContext for shell '{settings.Id}' has not been initialized yet. This may indicate a service is being resolved during shell construction."));
    }

    /// <summary>
    /// Configures services from each feature's startup in dependency order.
    /// </summary>
    /// <remarks>
    /// Features are instantiated using the root IServiceProvider plus ShellSettings
    /// as an explicit parameter. No temporary shell ServiceProviders are created during configuration.
    /// This ensures features can only depend on root-level services in their constructors.
    /// </remarks>
    private void ConfigureFeatureServices(ServiceCollection services, List<string> orderedFeatures, ShellSettings settings)
    {
        var featuresWithStartups = orderedFeatures
            .Select(name => (Name: name, Descriptor: _featureMap[name]))
            .Where(f => f.Descriptor.StartupType != null);

        foreach (var (featureName, descriptor) in featuresWithStartups)
        {
            ConfigureFeature(services, settings, featureName, descriptor);
        }
    }

    /// <summary>
    /// Configures services for a single feature by instantiating its startup and calling ConfigureServices.
    /// </summary>
    /// <remarks>
    /// The feature is instantiated using the root IServiceProvider (via ActivatorUtilities.CreateInstance)
    /// with ShellSettings passed as an explicit parameter. This means feature constructors can only
    /// depend on root-level services (logging, configuration, etc.) and ShellSettings.
    /// </remarks>
    private void ConfigureFeature(ServiceCollection services, ShellSettings settings, string featureName, ShellFeatureDescriptor descriptor)
    {
        try
        {
            // Create the feature instance using the root provider with ShellSettings as explicit parameter.
            // This ensures features can only depend on root-level services and ShellSettings, not shell services.
            var startup = CreateFeatureInstance(descriptor.StartupType!, settings);
            startup.ConfigureServices(services);

            _logger.LogDebug("Configured services from feature '{FeatureName}' startup type '{StartupType}'",
                featureName, descriptor.StartupType!.FullName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure services for feature '{FeatureName}'", featureName);
            throw new InvalidOperationException(
                $"Failed to configure services for feature '{featureName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates an instance of the specified feature type using the root provider.
    /// </summary>
    /// <param name="featureType">The feature type to instantiate.</param>
    /// <param name="shellSettings">The shell settings to pass as an explicit parameter.</param>
    /// <returns>The instantiated feature.</returns>
    /// <remarks>
    /// Uses ActivatorUtilities.CreateInstance with the root provider to instantiate
    /// the feature. ShellSettings is passed as an explicit parameter only if the constructor accepts it.
    /// </remarks>
    private IShellFeature CreateFeatureInstance(Type featureType, ShellSettings shellSettings)
    {
        // Check if any constructor accepts ShellSettings as a parameter
        var hasShellSettingsParameter = featureType.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Any(p => p.ParameterType == typeof(ShellSettings));

        if (hasShellSettingsParameter)
        {
            // Pass ShellSettings as explicit parameter - ActivatorUtilities will match it to the constructor parameter
            return (IShellFeature)ActivatorUtilities.CreateInstance(_rootProvider, featureType, shellSettings);
        }

        // No constructor accepts ShellSettings, create without explicit parameters
        return (IShellFeature)ActivatorUtilities.CreateInstance(_rootProvider, featureType);
    }

    /// <summary>
    /// A holder class that allows the ShellContext to be set after the service provider is built.
    /// This is an internal implementation detail and is not registered in the service collection.
    /// </summary>
    private sealed class ShellContextHolder
    {
        public ShellContext? Context { get; set; }
    }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> if this instance has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources used by this instance.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose all service providers
            var disposableProviders = _shellContexts.Values
                .Select(c => c.ServiceProvider)
                .OfType<IDisposable>();

            foreach (var disposable in disposableProviders)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing service provider");
                }
            }

            _shellContexts.Clear();
        }

        _disposed = true;
    }
}
