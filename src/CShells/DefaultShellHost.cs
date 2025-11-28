using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells;

/// <summary>
/// Default implementation of <see cref="IShellHost"/> that builds and caches per-shell
/// <see cref="IServiceProvider"/> instances.
/// </summary>
public class DefaultShellHost : IShellHost, IDisposable
{
    private readonly IReadOnlyDictionary<string, ShellFeatureDescriptor> _featureMap;
    private readonly IReadOnlyList<ShellSettings> _shellSettings;
    private readonly ConcurrentDictionary<ShellId, ShellContext> _shellContexts = new();
    private readonly FeatureDependencyResolver _dependencyResolver = new();
    private readonly ILogger<DefaultShellHost> _logger;
    private readonly Lock _buildLock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShellHost"/> class.
    /// </summary>
    /// <param name="shellSettings">The collection of shell settings to manage.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shellSettings"/> is null.</exception>
    public DefaultShellHost(IEnumerable<ShellSettings> shellSettings, ILogger<DefaultShellHost>? logger = null)
        : this(shellSettings, AppDomain.CurrentDomain.GetAssemblies(), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShellHost"/> class with custom assemblies.
    /// </summary>
    /// <param name="shellSettings">The collection of shell settings to manage.</param>
    /// <param name="assemblies">The assemblies to scan for features.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shellSettings"/> or <paramref name="assemblies"/> is null.</exception>
    public DefaultShellHost(IEnumerable<ShellSettings> shellSettings, IEnumerable<Assembly> assemblies, ILogger<DefaultShellHost>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(shellSettings);
        ArgumentNullException.ThrowIfNull(assemblies);
        
        _shellSettings = shellSettings.ToList();
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
    private IServiceProvider BuildServiceProvider(ShellSettings settings, List<string> orderedFeatures, ShellContextHolder contextHolder)
    {
        var services = new ServiceCollection();

        RegisterCoreServices(services, settings, contextHolder);

        if (orderedFeatures.Count == 0)
        {
            return services.BuildServiceProvider();
        }

        ConfigureFeatureServices(services, orderedFeatures);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Registers core services required by all shells.
    /// </summary>
    private static void RegisterCoreServices(ServiceCollection services, ShellSettings settings, ShellContextHolder contextHolder)
    {
        services.AddSingleton(settings);

        // Register the ShellContext using the holder pattern
        // The holder will be populated after the service provider is built
        services.AddSingleton<ShellContext>(sp => contextHolder.Context
            ?? throw new InvalidOperationException($"ShellContext for shell '{settings.Id}' has not been initialized yet. This may indicate a service is being resolved during shell construction."));
    }

    /// <summary>
    /// Configures services from each feature's startup in dependency order.
    /// Each feature is instantiated with a temp provider that includes services from all previously-processed features.
    /// </summary>
    private void ConfigureFeatureServices(ServiceCollection services, List<string> orderedFeatures)
    {
        var featuresWithStartups = orderedFeatures
            .Select(name => (Name: name, Descriptor: _featureMap[name]))
            .Where(f => f.Descriptor.StartupType != null);

        foreach (var (featureName, descriptor) in featuresWithStartups)
        {
            ConfigureFeature(services, featureName, descriptor);
        }
    }

    /// <summary>
    /// Configures services for a single feature by instantiating its startup and calling ConfigureServices.
    /// </summary>
    private void ConfigureFeature(ServiceCollection services, string featureName, ShellFeatureDescriptor descriptor)
    {
        try
        {
            // Create a temporary service provider that includes services registered so far
            // This allows each startup to depend on services from its dependency features
            using var tempProvider = services.BuildServiceProvider();

            var startup = (IShellFeature)ActivatorUtilities.CreateInstance(tempProvider, descriptor.StartupType!);
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
