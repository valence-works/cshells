using CShells.AspNetCore.Features;
using CShells.AspNetCore.Middleware;
using CShells.AspNetCore.Resolution;
using CShells.Features;
using CShells.Hosting;
using CShells.Resolution;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring CShells middleware in the ASP.NET Core pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    // Static flag to ensure web shell features are only configured once per process.
    private static bool _webShellFeaturesConfigured;

    // Lock object for thread-safe initialization of web shell features.
    private static readonly Lock ConfigurationLock = new();

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
        // ReSharper disable once InconsistentlySynchronizedField
        if (_webShellFeaturesConfigured)
            return;

        lock (ConfigurationLock)
        {
            // Double-check after acquiring lock.
            if (_webShellFeaturesConfigured)
                return;

            _webShellFeaturesConfigured = true;

            var rootProvider = app.ApplicationServices;
            var environment = rootProvider.GetService<IHostEnvironment>();
            var loggerFactory = rootProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger(typeof(ApplicationBuilderExtensions)) ?? NullLogger.Instance;

            var descriptors = DiscoverWebShellFeatures(logger);
            if (descriptors == null)
                return;

            var (enabledFeatureIds, shouldFilterFeatures) = GetEnabledFeatureIds(rootProvider, logger);
            var pathMappings = GetPathMappings(rootProvider);

            foreach (var descriptor in descriptors)
            {
                if (shouldFilterFeatures && !enabledFeatureIds.Contains(descriptor.Id))
                {
                    logger.LogDebug("Skipping web shell feature '{FeatureId}' as it is not enabled for any shell", descriptor.Id);
                    continue;
                }

                ConfigureWebShellFeature(app, rootProvider, environment, descriptor, pathMappings, logger);
            }
        }
    }

    /// <summary>
    /// Discovers and filters web shell features from all loaded assemblies.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A list of web shell feature descriptors, or null if discovery fails.</returns>
    private static IReadOnlyList<ShellFeatureDescriptor>? DiscoverWebShellFeatures(ILogger logger)
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic);
            var allDescriptors = FeatureDiscovery.DiscoverFeatures(assemblies);

            return allDescriptors
                .Where(d => d.StartupType is not null && typeof(IWebShellFeature).IsAssignableFrom(d.StartupType))
                .OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Failed to discover shell features for web configuration. Web shell features will not be configured.");
            return null;
        }
    }

    /// <summary>
    /// Configures a single web shell feature for all applicable shells.
    /// </summary>
    private static void ConfigureWebShellFeature(
        IApplicationBuilder app,
        IServiceProvider rootProvider,
        IHostEnvironment? environment,
        ShellFeatureDescriptor descriptor,
        Dictionary<string, ShellId> pathMappings,
        ILogger logger)
    {
        try
        {
            var featureFactory = rootProvider.GetRequiredService<IShellFeatureFactory>();

            if (pathMappings.Count > 0)
            {
                ConfigureFeatureWithPathMappings(app, rootProvider, environment, descriptor, pathMappings, featureFactory);
            }
            else
            {
                ConfigureFeatureGlobally(app, rootProvider, environment, descriptor, featureFactory);
            }

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

    /// <summary>
    /// Configures a feature for shells with path mappings (e.g., /tenant1, /tenant2).
    /// </summary>
    private static void ConfigureFeatureWithPathMappings(
        IApplicationBuilder app,
        IServiceProvider rootProvider,
        IHostEnvironment? environment,
        ShellFeatureDescriptor descriptor,
        Dictionary<string, ShellId> pathMappings,
        IShellFeatureFactory featureFactory)
    {
        foreach (var (path, shellId) in pathMappings)
        {
            if (!IsFeatureEnabledForShell(rootProvider, shellId, descriptor.Id))
                continue;

            var shellSettings = GetShellSettings(rootProvider, shellId);
            var feature = featureFactory.CreateFeature<IWebShellFeature>(descriptor.StartupType!, shellSettings);
            var pathPrefix = string.IsNullOrEmpty(path) ? "" : "/" + path;
            app.Map(pathPrefix, branch => feature.Configure(branch, environment));
        }
    }

    /// <summary>
    /// Configures a feature globally for all shells without path mappings.
    /// </summary>
    private static void ConfigureFeatureGlobally(
        IApplicationBuilder app,
        IServiceProvider rootProvider,
        IHostEnvironment? environment,
        ShellFeatureDescriptor descriptor,
        IShellFeatureFactory featureFactory)
    {
        var shellHost = rootProvider.GetService<IShellHost>();
        if (shellHost != null)
        {
            foreach (var shell in shellHost.AllShells)
            {
                if (shell.Settings.EnabledFeatures.Contains(descriptor.Id, StringComparer.OrdinalIgnoreCase))
                {
                    var feature = featureFactory.CreateFeature<IWebShellFeature>(descriptor.StartupType!, shell.Settings);
                    feature.Configure(app, environment);
                }
            }
        }
        else
        {
            // Fallback: No shell host - instantiate without shell settings
            var feature = featureFactory.CreateFeature<IWebShellFeature>(descriptor.StartupType!, shellSettings: null);
            feature.Configure(app, environment);
        }
    }

    private static Dictionary<string, ShellId> GetPathMappings(IServiceProvider serviceProvider)
    {
        var mappings = new Dictionary<string, ShellId>(StringComparer.OrdinalIgnoreCase);

        // Get path mappings from shell properties
        var shellHost = serviceProvider.GetService<IShellHost>();
        if (shellHost != null)
        {
            foreach (var shell in shellHost.AllShells)
            {
                if (shell.Settings.Properties.TryGetValue(ShellPropertyKeys.Path, out var pathValue))
                {
                    // Handle both string and JsonElement values (from JSON deserialization)
                    var path = pathValue switch
                    {
                        string s => s,
                        System.Text.Json.JsonElement jsonElement when jsonElement.ValueKind == System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                        _ => null
                    };

                    if (path != null)
                    {
                        mappings[path] = shell.Id;
                    }
                }
            }
        }

        return mappings;
    }

    private static ShellContext? GetShellContext(IServiceProvider rootProvider, ShellId shellId)
    {
        var shellHost = rootProvider.GetService<IShellHost>();
        return shellHost?.AllShells.FirstOrDefault(s => s.Id == shellId);
    }

    private static bool IsFeatureEnabledForShell(IServiceProvider rootProvider, ShellId shellId, string featureId)
    {
        var shell = GetShellContext(rootProvider, shellId);
        return shell?.Settings.EnabledFeatures.Contains(featureId, StringComparer.OrdinalIgnoreCase) ?? true;
    }

    private static ShellSettings? GetShellSettings(IServiceProvider rootProvider, ShellId shellId)
    {
        return GetShellContext(rootProvider, shellId)?.Settings;
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
