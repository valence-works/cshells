using CShells.AspNetCore.Features;
using CShells.AspNetCore.Resolution;
using CShells.AspNetCore.Routing;
using CShells.Features;
using CShells.Hosting;
using CShells.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.AspNetCore.Notifications;

/// <summary>
/// Handles shell lifecycle notifications by registering/removing endpoints in the dynamic endpoint data source.
/// </summary>
public class ShellEndpointRegistrationHandler :
    INotificationHandler<ShellAddedNotification>,
    INotificationHandler<ShellRemovedNotification>,
    INotificationHandler<ShellsReloadedNotification>
{
    private readonly DynamicShellEndpointDataSource _endpointDataSource;
    private readonly EndpointRouteBuilderAccessor _endpointRouteBuilderAccessor;
    private readonly IShellHost _shellHost;
    private readonly IShellFeatureFactory _featureFactory;
    private readonly IHostEnvironment? _environment;
    private readonly ILogger<ShellEndpointRegistrationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellEndpointRegistrationHandler"/> class.
    /// </summary>
    public ShellEndpointRegistrationHandler(
        DynamicShellEndpointDataSource endpointDataSource,
        IShellHost shellHost,
        IShellFeatureFactory featureFactory,
        EndpointRouteBuilderAccessor endpointRouteBuilderAccessor,
        IHostEnvironment? environment = null,
        ILogger<ShellEndpointRegistrationHandler>? logger = null)
    {
        _endpointDataSource = endpointDataSource;
        _endpointRouteBuilderAccessor = endpointRouteBuilderAccessor;
        _shellHost = shellHost;
        _featureFactory = featureFactory;
        _environment = environment;
        _logger = logger ?? NullLogger<ShellEndpointRegistrationHandler>.Instance;
    }

    /// <inheritdoc />
    public Task HandleAsync(ShellAddedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_endpointRouteBuilderAccessor.EndpointRouteBuilder == null)
        {
            _logger.LogWarning("Cannot register endpoints for shell '{ShellId}': IEndpointRouteBuilder not available. " +
                              "Endpoints will be registered on next application start.", notification.Settings.Id);
            return Task.CompletedTask;
        }

        _logger.LogInformation("Registering endpoints for shell '{ShellId}'", notification.Settings.Id);
        RegisterShellEndpoints(notification.Settings);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HandleAsync(ShellRemovedNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Removing endpoints for shell '{ShellId}'", notification.ShellId);
        _endpointDataSource.RemoveEndpoints(notification.ShellId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HandleAsync(ShellsReloadedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_endpointRouteBuilderAccessor.EndpointRouteBuilder == null)
        {
            _logger.LogWarning("Cannot register endpoints: IEndpointRouteBuilder not available. " +
                              "This typically means the application hasn't been fully configured yet.");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Registering endpoints for {Count} shell(s)", notification.AllShells.Count);

        // Clear existing endpoints
        _endpointDataSource.Clear();

        // Register endpoints for all shells
        foreach (var settings in notification.AllShells)
        {
            RegisterShellEndpoints(settings);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers endpoints for a single shell.
    /// </summary>
    private void RegisterShellEndpoints(ShellSettings settings)
    {
        var endpointRouteBuilder = _endpointRouteBuilderAccessor.EndpointRouteBuilder;
        if (endpointRouteBuilder == null)
            return;

        _logger.LogDebug("Registering endpoints for shell '{ShellId}'", settings.Id);

        // Get path prefix from shell properties
        _logger.LogInformation("Shell '{ShellId}' has {PropertyCount} properties",
            settings.Id, settings.Properties.Count);

        if (settings.Properties.TryGetValue(ShellPropertyKeys.WebRouting, out var pathValue))
        {
            _logger.LogInformation("Shell '{ShellId}' WebRouting property type: {TypeName}, value: {Value}",
                settings.Id, pathValue?.GetType().Name ?? "null", pathValue);
        }
        else
        {
            _logger.LogWarning("Shell '{ShellId}' does not have property '{PropertyKey}'",
                settings.Id, ShellPropertyKeys.WebRouting);
        }

        var pathPrefix = GetPathPrefix(settings);

        _logger.LogInformation("Shell '{ShellId}' path prefix: '{PathPrefix}'", settings.Id, pathPrefix ?? "(none)");

        // Create shell-scoped endpoint builder
        var shellEndpointBuilder = new ShellEndpointRouteBuilder(
            endpointRouteBuilder,
            settings.Id,
            settings,
            pathPrefix);

        // Get shell context
        var shellContext = _shellHost.GetShell(settings.Id);

        // Discover web features
        var webFeatures = DiscoverWebFeatures(settings);

        // Map endpoints for each web feature
        foreach (var (featureId, featureType) in webFeatures)
        {
            try
            {
                var feature = _featureFactory.CreateFeature<IWebShellFeature>(featureType, settings);
                feature.MapEndpoints(shellEndpointBuilder, _environment);

                _logger.LogDebug("Mapped endpoints for feature '{FeatureId}' in shell '{ShellId}'",
                    featureId, settings.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to map endpoints for feature '{FeatureId}' in shell '{ShellId}'",
                    featureId, settings.Id);
                throw;
            }
        }

        // Add all endpoints to the data source
        var shellEndpoints = shellEndpointBuilder.GetEndpoints().ToList();

        // Log the actual route patterns being registered
        foreach (var endpoint in shellEndpoints)
        {
            if (endpoint is RouteEndpoint routeEndpoint)
            {
                _logger.LogInformation("Registering endpoint for shell '{ShellId}': {RoutePattern}",
                    settings.Id, routeEndpoint.RoutePattern.RawText);
            }
        }

        _endpointDataSource.AddEndpoints(shellEndpoints);

        _logger.LogDebug("Registered {Count} endpoint(s) for shell '{ShellId}'",
            shellEndpoints.Count, settings.Id);
    }

    /// <summary>
    /// Discovers web features that are enabled for a shell.
    /// </summary>
    private static IEnumerable<(string FeatureId, Type FeatureType)> DiscoverWebFeatures(ShellSettings settings)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic);
        var allFeatures = FeatureDiscovery.DiscoverFeatures(assemblies);

        return allFeatures
            .Where(d => d.StartupType != null &&
                        typeof(IWebShellFeature).IsAssignableFrom(d.StartupType) &&
                        settings.EnabledFeatures.Contains(d.Id, StringComparer.OrdinalIgnoreCase))
            .Select(d => (d.Id, d.StartupType!));
    }

    /// <summary>
    /// Gets the path prefix for a shell from its properties.
    /// </summary>
    private static string? GetPathPrefix(ShellSettings settings)
    {
        var routingOptions = settings.GetProperty<WebRoutingShellOptions>(ShellPropertyKeys.WebRouting);
        if (routingOptions == null)
            return null;

        var path = routingOptions.Path;

        // Null means no path routing configured for this shell
        if (path == null)
            return null;

        // Empty string is valid and means root path (no prefix)
        if (path == string.Empty)
            return null; // Return null for empty path to indicate no prefix

        var trimmedPath = path.Trim();
        if (!trimmedPath.StartsWith('/'))
            trimmedPath = "/" + trimmedPath;
        if (trimmedPath.EndsWith('/') && trimmedPath.Length > 1)
            trimmedPath = trimmedPath.TrimEnd('/');

        return trimmedPath;
    }
}
