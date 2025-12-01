using CShells.AspNetCore.Management;
using CShells.AspNetCore.Middleware;
using CShells.AspNetCore.Routing;
using CShells.Configuration;
using CShells.Features;
using CShells.Hosting;
using CShells.Management;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring CShells middleware and endpoint routing.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds CShells endpoint routing to the application pipeline.
    /// This must be called after UseRouting() and before UseEndpoints().
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method configures dynamic endpoint routing for multi-tenant shell applications.
    /// Shells can be loaded at startup from configuration or asynchronously from storage,
    /// and can be added, removed, or updated at runtime without restarting the application.
    /// </para>
    /// <para>
    /// Proper usage:
    /// <code>
    /// app.UseRouting();
    /// app.UseCShells();
    /// app.UseEndpoints(endpoints => { endpoints.MapCShells(); });
    /// </code>
    /// </para>
    /// </remarks>
    public static IApplicationBuilder UseCShells(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var logger = app.ApplicationServices.GetService<ILoggerFactory>()
            ?.CreateLogger(typeof(ApplicationBuilderExtensions))
            ?? NullLogger.Instance;

        logger.LogInformation("Configuring CShells endpoint routing");

        // Add shell resolution middleware (sets current shell context on request)
        app.UseMiddleware<ShellMiddleware>();

        logger.LogInformation("CShells endpoint routing configured successfully");

        return app;
    }

    /// <summary>
    /// Configures CShells endpoints. This should be called within UseEndpoints().
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint convention builder.</returns>
    /// <example>
    /// <code>
    /// app.UseEndpoints(endpoints =>
    /// {
    ///     endpoints.MapCShells();
    ///     endpoints.MapControllers();
    /// });
    /// </code>
    /// </example>
    public static IEndpointConventionBuilder MapCShells(this IEndpointRouteBuilder endpoints)
    {
        var logger = endpoints.ServiceProvider.GetService<ILoggerFactory>()
            ?.CreateLogger(typeof(ApplicationBuilderExtensions))
            ?? NullLogger.Instance;

        logger.LogInformation("Mapping CShells endpoints");

        // Capture the endpoint route builder in the accessor so notification handlers can use it
        var accessor = endpoints.ServiceProvider.GetRequiredService<EndpointRouteBuilderAccessor>();
        accessor.EndpointRouteBuilder = endpoints;

        // Get the dynamic endpoint data source
        var endpointDataSource = endpoints.ServiceProvider.GetRequiredService<DynamicShellEndpointDataSource>();

        // Add the data source to the endpoint route builder
        // This makes all shell endpoints available to the routing system
        endpoints.DataSources.Add(endpointDataSource);

        // Register endpoints for any shells already in the cache
        // This handles shells loaded synchronously from configuration at startup
        RegisterEndpointsForCachedShells(endpoints.ServiceProvider, logger);

        logger.LogInformation("CShells endpoints mapped successfully");

        // Return a convention builder (even though we don't have specific conventions to apply)
        return new EndpointConventionBuilder(endpointDataSource);
    }

    /// <summary>
    /// Registers endpoints for shells by loading them from the provider if not already in cache.
    /// This is called once during application startup to handle shells loaded from configuration.
    /// </summary>
    private static void RegisterEndpointsForCachedShells(IServiceProvider serviceProvider, ILogger logger)
    {
        logger.LogDebug("Loading shells for endpoint registration");

        var cache = serviceProvider.GetRequiredService<IShellSettingsCache>();
        var provider = serviceProvider.GetRequiredService<IShellSettingsProvider>();
        var notificationPublisher = serviceProvider.GetRequiredService<CShells.Notifications.INotificationPublisher>();

        // Check if cache is already populated
        var allSettings = cache.GetAll().ToList();

        // If cache is empty, load shells synchronously from the provider
        // This ensures endpoints are registered before the application starts accepting requests
        if (allSettings.Count == 0)
        {
            logger.LogDebug("Cache is empty, loading shells from provider");
            var settingsTask = provider.GetShellSettingsAsync(CancellationToken.None);
            settingsTask.Wait(); // Synchronous wait during startup is acceptable
            allSettings = settingsTask.Result.ToList();

            if (allSettings.Count > 0)
            {
                // Load shells into cache (cast to concrete type since IShellSettingsCache doesn't expose Load)
                if (cache is ShellSettingsCache concreteCache)
                {
                    concreteCache.Load(allSettings);
                    logger.LogInformation("Loaded {Count} shell(s) from provider into cache", allSettings.Count);
                }
                else
                {
                    logger.LogWarning("Unable to load shells into cache: cache is not ShellSettingsCache type");
                    return;
                }
            }
            else
            {
                logger.LogDebug("No shells available from provider. Endpoints will be registered when shells are added.");
                return;
            }
        }
        else
        {
            logger.LogDebug("Using {Count} shell(s) already in cache", allSettings.Count);
        }

        logger.LogInformation("Registering endpoints for {Count} shell(s)", allSettings.Count);

        // Publish notification synchronously to register endpoints via the notification handler
        // This uses the same code path as dynamic shell loading
        var notification = new CShells.Notifications.ShellsReloadedNotification(allSettings);
        notificationPublisher.PublishAsync(notification, strategy: null, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        logger.LogInformation("Shell endpoint registration complete");
    }

    /// <summary>
    /// Gets the path prefix for a shell from its properties.
    /// </summary>
    private static string? GetPathPrefix(ShellSettings settings)
    {
        if (settings.Properties.TryGetValue(ShellPropertyKeys.Path, out var pathObj) &&
            pathObj is string path &&
            !string.IsNullOrWhiteSpace(path))
        {
            var trimmedPath = path.Trim();
            if (!trimmedPath.StartsWith('/'))
                trimmedPath = "/" + trimmedPath;
            if (trimmedPath.EndsWith('/') && trimmedPath.Length > 1)
                trimmedPath = trimmedPath.TrimEnd('/');

            return trimmedPath;
        }

        return null;
    }

    /// <summary>
    /// A simple endpoint convention builder for shell endpoints.
    /// </summary>
    private class EndpointConventionBuilder : IEndpointConventionBuilder
    {
        private readonly DynamicShellEndpointDataSource _dataSource;

        public EndpointConventionBuilder(DynamicShellEndpointDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public void Add(Action<EndpointBuilder> convention)
        {
            // Conventions can be applied to all endpoints in the data source
            // For now, we don't need to support this, but it's here for extensibility
        }
    }
}
