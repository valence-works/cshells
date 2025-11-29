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
    /// <see cref="IWebShellFeature"/> implementations.
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
    ///     available via <see cref="IShellHost"/> and calls their <see cref="IWebShellFeature.Configure"/>
    ///     method to configure the application pipeline.
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
    /// Configures all discovered <see cref="IWebShellFeature"/> implementations.
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

            // Apply deterministic ordering by feature Id (case-insensitive)
            var orderedDescriptors = descriptors
                .OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var descriptor in orderedDescriptors)
            {
                // Skip features without a startup type
                if (descriptor.StartupType is null)
                    continue;

                // Only process types implementing IWebShellFeature
                if (!typeof(IWebShellFeature).IsAssignableFrom(descriptor.StartupType))
                    continue;

                try
                {
                    // Instantiate using root provider (feature can depend on root-level services)
                    var feature = (IWebShellFeature)ActivatorUtilities.CreateInstance(rootProvider, descriptor.StartupType);

                    // Configure the application pipeline for this web feature
                    feature.Configure(app, environment!);

                    logger.LogDebug("Configured web shell feature '{FeatureId}' from type '{FeatureType}'",
                        descriptor.Id, descriptor.StartupType.FullName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to configure web shell feature '{FeatureId}' from type '{FeatureType}'",
                        descriptor.Id, descriptor.StartupType.FullName);
                    throw;
                }
            }
        }
    }
}
