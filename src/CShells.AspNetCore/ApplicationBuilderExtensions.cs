using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.AspNetCore;

/// <summary>
/// Extension methods for configuring CShells middleware in the ASP.NET Core pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    // Static flag to ensure web shell features are only configured once per process.
    private static bool _webShellFeaturesConfigured;

    // Lock object for thread-safe initialization of web shell features.
    private static readonly object _configurationLock = new();

    /// <summary>
    /// Adds the CShells middleware to the application pipeline and configures all discovered
    /// <see cref="IWebShellFeature"/> implementations that are enabled for at least one shell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method performs two main operations:
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     <description>
    ///     Adds <see cref="ShellMiddleware"/> which resolves the current shell for each request
    ///     and sets the appropriate <see cref="Microsoft.AspNetCore.Http.HttpContext.RequestServices"/> scope.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     Discovers all feature types implementing <see cref="IWebShellFeature"/> from feature descriptors
    ///     and calls their <see cref="IWebShellFeature.Configure"/> method to configure the application pipeline,
    ///     but only for features that are enabled for at least one shell. This prevents runtime errors
    ///     when endpoints are mapped for features whose services are not registered.
    ///     </description>
    ///   </item>
    /// </list>
    /// <para>
    /// <see cref="IWebShellFeature"/> implementations are instantiated using the root <see cref="IServiceProvider"/>
    /// via <see cref="ActivatorUtilities.CreateInstance"/>, allowing constructors to depend on root-level services.
    /// </para>
    /// <para>
    /// Web shell feature configuration is idempotent: even if <c>UseCShells()</c> is called multiple times,
    /// the <see cref="IWebShellFeature.Configure"/> methods will only be invoked once per process.
    /// Features are configured in deterministic order (by feature name, case-insensitive ascending).
    /// </para>
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseCShells(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<ShellMiddleware>();

        ConfigureWebShellFeatures(app);

        return app;
    }

    /// <summary>
    /// Configures all discovered <see cref="IWebShellFeature"/> implementations that are enabled for at least one shell.
    /// This method is idempotent and will only execute feature configuration once per process.
    /// </summary>
    /// <param name="app">The application builder.</param>
    private static void ConfigureWebShellFeatures(IApplicationBuilder app)
    {
        // Fast path: skip if already configured
        if (_webShellFeaturesConfigured)
            return;

        lock (_configurationLock)
        {
            // Double-check after acquiring lock
            if (_webShellFeaturesConfigured)
                return;

            _webShellFeaturesConfigured = true;

            var rootProvider = app.ApplicationServices;
            var environment = rootProvider.GetService<IHostEnvironment>();
            var loggerFactory = rootProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger(typeof(ApplicationBuilderExtensions))
                ?? NullLogger.Instance;

            IEnumerable<ShellFeatureDescriptor> descriptors;
            try
            {
                // Get features from FeatureDiscovery using all loaded assemblies,
                // excluding dynamic assemblies which may contain test fixtures with invalid configurations.
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic);
                descriptors = FeatureDiscovery.DiscoverFeatures(assemblies);
            }
            catch (InvalidOperationException ex)
            {
                // Feature discovery can fail if there are duplicate features or other configuration issues.
                // Log the error and continue without configuring web features.
                logger.LogWarning(ex, "Failed to discover shell features for web configuration. Web shell features will not be configured.");
                return;
            }

            // Get enabled features from all shells to only configure web features that are actually used
            var (enabledFeatureIds, shouldFilterFeatures) = GetEnabledFeatureIds(rootProvider, logger);

            // Filter and partition web shell features in a single pass
            var webShellFeatureDescriptors = descriptors
                .Where(d => d.StartupType is not null && typeof(IWebShellFeature).IsAssignableFrom(d.StartupType))
                .OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var descriptor in webShellFeatureDescriptors)
            {
                // Skip features that are not enabled for any shell (only if we should filter)
                if (shouldFilterFeatures && !enabledFeatureIds.Contains(descriptor.Id))
                {
                    logger.LogDebug("Skipping web shell feature '{FeatureId}' as it is not enabled for any shell", descriptor.Id);
                    continue;
                }

                try
                {
                    // Instantiate using root provider (feature can depend on root-level services)
                    var feature = (IWebShellFeature)ActivatorUtilities.CreateInstance(rootProvider, descriptor.StartupType!);

                    // Configure the application pipeline for this web feature.
                    // Note: environment may be null if IHostEnvironment is not registered;
                    // features must handle null environments gracefully.
                    feature.Configure(app, environment);

                    logger.LogDebug("Configured web shell feature '{FeatureId}' from type '{FeatureType}'",
                        descriptor.Id, descriptor.StartupType!.FullName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to configure web shell feature '{FeatureId}' from type '{FeatureType}'",
                        descriptor.Id, descriptor.StartupType!.FullName);
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Gets the set of feature IDs that are enabled for at least one shell.
    /// </summary>
    /// <param name="rootProvider">The root service provider.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>
    /// A tuple containing:
    /// - A hash set of enabled feature IDs (case-insensitive)
    /// - A boolean indicating whether features should be filtered (false means configure all features for backwards compatibility)
    /// </returns>
    private static (HashSet<string> EnabledFeatureIds, bool ShouldFilter) GetEnabledFeatureIds(IServiceProvider rootProvider, ILogger logger)
    {
        var shellHost = rootProvider.GetService<IShellHost>();
        if (shellHost is null)
        {
            logger.LogWarning("IShellHost not registered in service provider. All discovered web features will be configured.");
            // Don't filter - configure all features (backwards compatibility)
            return ([], false);
        }

        try
        {
            // Collect all enabled feature IDs from all shells
            var enabledFeatureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var shell in shellHost.AllShells)
            {
                foreach (var featureId in shell.Settings.EnabledFeatures)
                {
                    enabledFeatureIds.Add(featureId);
                }
            }

            logger.LogDebug("Found {Count} unique enabled features across all shells", enabledFeatureIds.Count);
            return (enabledFeatureIds, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve enabled features from shell host. All discovered web features will be configured.");
            // Don't filter - configure all features (backwards compatibility)
            return ([], false);
        }
    }
}
