using CShells.AspNetCore.Features;
using CShells.AspNetCore.Routing;
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.AspNetCore.Notifications;

/// <summary>
/// Reacts to shell lifecycle transitions by (re-)registering or removing endpoints + middleware
/// in the dynamic endpoint data source. Subscribed to the registry via
/// <see cref="IShellLifecycleSubscriber"/>.
/// </summary>
public sealed class ShellEndpointRegistrationHandler : IShellLifecycleSubscriber
{
    private readonly DynamicShellEndpointDataSource _endpointDataSource;
    private readonly EndpointRouteBuilderAccessor _endpointRouteBuilderAccessor;
    private readonly ApplicationBuilderAccessor _applicationBuilderAccessor;
    private readonly IShellFeatureFactory _featureFactory;
    private readonly IHostEnvironment? _environment;
    private readonly ILogger<ShellEndpointRegistrationHandler> _logger;

    public ShellEndpointRegistrationHandler(
        DynamicShellEndpointDataSource endpointDataSource,
        IShellFeatureFactory featureFactory,
        EndpointRouteBuilderAccessor endpointRouteBuilderAccessor,
        ApplicationBuilderAccessor applicationBuilderAccessor,
        IHostEnvironment? environment = null,
        ILogger<ShellEndpointRegistrationHandler>? logger = null)
    {
        _endpointDataSource = Guard.Against.Null(endpointDataSource);
        _endpointRouteBuilderAccessor = Guard.Against.Null(endpointRouteBuilderAccessor);
        _applicationBuilderAccessor = Guard.Against.Null(applicationBuilderAccessor);
        _featureFactory = Guard.Against.Null(featureFactory);
        _environment = environment;
        _logger = logger ?? NullLogger<ShellEndpointRegistrationHandler>.Instance;
    }

    /// <inheritdoc />
    public Task OnStateChangedAsync(IShell shell, ShellLifecycleState previous, ShellLifecycleState current, CancellationToken cancellationToken = default)
    {
        // Register when a shell becomes Active, tear down when it starts deactivating.
        if (previous == ShellLifecycleState.Initializing && current == ShellLifecycleState.Active)
        {
            if (_endpointRouteBuilderAccessor.EndpointRouteBuilder is null)
            {
                _logger.LogWarning(
                    "Cannot register endpoints for shell '{Shell}': IEndpointRouteBuilder not available. " +
                    "Endpoints will be registered on next application start.",
                    shell.Descriptor);
                return Task.CompletedTask;
            }

            _logger.LogInformation("Registering endpoints for active shell '{Shell}'", shell.Descriptor);
            var shellId = new ShellId(shell.Descriptor.Name);
            _endpointDataSource.RemoveEndpoints(shellId);
            RegisterShellEndpoints(shell);
            return Task.CompletedTask;
        }

        if (current == ShellLifecycleState.Deactivating || current == ShellLifecycleState.Disposed)
        {
            _logger.LogInformation("Removing endpoints for shell '{Shell}' generation {Generation} ({State})",
                shell.Descriptor, shell.Descriptor.Generation, current);
            _endpointDataSource.RemoveEndpoints(new ShellId(shell.Descriptor.Name), shell.Descriptor.Generation);
        }

        return Task.CompletedTask;
    }

    private void RegisterShellEndpoints(IShell shell)
    {
        var endpointRouteBuilder = _endpointRouteBuilderAccessor.EndpointRouteBuilder;
        if (endpointRouteBuilder is null)
            return;

        var settings = shell.ServiceProvider.GetRequiredService<ShellSettings>();
        _logger.LogDebug("Registering endpoints for shell '{Shell}' ({FeatureCount} config entries)",
            shell.Descriptor, settings.ConfigurationData.Count);

        var shellPathPrefix = GetPathPrefix(settings);
        var routePrefix = GetRoutePrefix(settings);
        var combinedPrefix = CombinePrefixes(shellPathPrefix, routePrefix);

        _logger.LogInformation("Shell '{Shell}' path prefix: '{PathPrefix}', route prefix: '{RoutePrefix}', combined: '{Combined}'",
            shell.Descriptor,
            shellPathPrefix ?? "(none)",
            routePrefix ?? "(none)",
            combinedPrefix ?? "(none)");

        var shellEndpointBuilder = new ShellEndpointRouteBuilder(
            endpointRouteBuilder,
            settings.Id,
            shell.Descriptor.Generation,
            settings,
            shell.ServiceProvider,
            combinedPrefix);

        var allFeatureDescriptors = shell.ServiceProvider.GetRequiredService<IEnumerable<ShellFeatureDescriptor>>().ToList();
        var featureContext = new ShellFeatureContext(settings, allFeatureDescriptors.AsReadOnly());

        RegisterShellMiddleware(settings, shell, allFeatureDescriptors, featureContext, shellPathPrefix);

        var webFeatures = DiscoverWebFeatures(settings, allFeatureDescriptors);
        foreach (var (featureId, featureType) in webFeatures)
        {
            try
            {
                var feature = _featureFactory.CreateFeature<IWebShellFeature>(featureType, settings, featureContext);
                feature.MapEndpoints(shellEndpointBuilder, _environment);

                _logger.LogDebug("Mapped endpoints for feature '{FeatureId}' in shell '{Shell}'",
                    featureId, shell.Descriptor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to map endpoints for feature '{FeatureId}' in shell '{Shell}'",
                    featureId, shell.Descriptor);
                throw;
            }
        }

        var shellEndpoints = shellEndpointBuilder.GetEndpoints().ToList();

        foreach (var endpoint in shellEndpoints)
        {
            if (endpoint is RouteEndpoint routeEndpoint)
            {
                _logger.LogInformation("Registering endpoint for shell '{Shell}': {RoutePattern}",
                    shell.Descriptor, routeEndpoint.RoutePattern.RawText);
            }
        }

        _endpointDataSource.AddEndpoints(shellEndpoints);
        _logger.LogDebug("Registered {Count} endpoint(s) for shell '{Shell}'", shellEndpoints.Count, shell.Descriptor);
    }

    private static IEnumerable<(string FeatureId, Type FeatureType)> DiscoverWebFeatures(
        ShellSettings settings,
        IEnumerable<ShellFeatureDescriptor> allFeatureDescriptors)
    {
        var enabled = new HashSet<string>(settings.EnabledFeatures, StringComparer.OrdinalIgnoreCase);

        return allFeatureDescriptors
            .Where(d => d.StartupType is not null &&
                        typeof(IWebShellFeature).IsAssignableFrom(d.StartupType) &&
                        enabled.Contains(d.Id))
            .Select(d => (d.Id, d.StartupType!));
    }

    private static IEnumerable<(string FeatureId, Type FeatureType)> DiscoverMiddlewareFeatures(
        ShellSettings settings,
        IEnumerable<ShellFeatureDescriptor> allFeatureDescriptors)
    {
        var enabled = new HashSet<string>(settings.EnabledFeatures, StringComparer.OrdinalIgnoreCase);

        return allFeatureDescriptors
            .Where(d => d.StartupType is not null &&
                        typeof(IMiddlewareShellFeature).IsAssignableFrom(d.StartupType) &&
                        enabled.Contains(d.Id))
            .Select(d => (d.Id, d.StartupType!));
    }

    private void RegisterShellMiddleware(
        ShellSettings settings,
        IShell shell,
        IReadOnlyCollection<ShellFeatureDescriptor> allFeatureDescriptors,
        ShellFeatureContext featureContext,
        string? shellPathPrefix)
    {
        var appBuilder = _applicationBuilderAccessor.ApplicationBuilder;
        if (appBuilder is null)
        {
            _logger.LogDebug("IApplicationBuilder not available, skipping middleware registration for shell '{Shell}'", shell.Descriptor);
            return;
        }

        var middlewareFeatures = DiscoverMiddlewareFeatures(settings, allFeatureDescriptors).ToList();
        if (middlewareFeatures.Count == 0)
            return;

        _logger.LogInformation("Registering middleware for {Count} feature(s) in shell '{Shell}'",
            middlewareFeatures.Count, shell.Descriptor);

        foreach (var (featureId, featureType) in middlewareFeatures)
        {
            try
            {
                var feature = _featureFactory.CreateFeature<IMiddlewareShellFeature>(featureType, settings, featureContext);

                if (!string.IsNullOrEmpty(shellPathPrefix))
                {
                    appBuilder.Map(shellPathPrefix, branch => feature.UseMiddleware(branch, _environment));
                }
                else
                {
                    feature.UseMiddleware(appBuilder, _environment);
                }

                _logger.LogDebug("Registered middleware for feature '{FeatureId}' in shell '{Shell}'", featureId, shell.Descriptor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register middleware for feature '{FeatureId}' in shell '{Shell}'", featureId, shell.Descriptor);
                throw;
            }
        }
    }

    private static string? GetPathPrefix(ShellSettings settings)
    {
        var path = settings.GetConfiguration("WebRouting:Path");
        if (path is null)
            return null;
        if (path == string.Empty)
            return null;

        var trimmedPath = path.Trim();
        if (!trimmedPath.StartsWith('/')) trimmedPath = "/" + trimmedPath;
        if (trimmedPath.EndsWith('/') && trimmedPath.Length > 1) trimmedPath = trimmedPath.TrimEnd('/');
        return trimmedPath;
    }

    private static string? GetRoutePrefix(ShellSettings settings)
    {
        const string routePrefixKey = "WebRouting:RoutePrefix";
        if (settings.ConfigurationData.TryGetValue(routePrefixKey, out var prefix) && prefix is not null)
        {
            var prefixStr = prefix.ToString();
            if (string.IsNullOrWhiteSpace(prefixStr))
                return null;

            var trimmedPrefix = prefixStr.Trim();
            if (trimmedPrefix.StartsWith('/')) trimmedPrefix = trimmedPrefix.TrimStart('/');
            if (trimmedPrefix.EndsWith('/')) trimmedPrefix = trimmedPrefix.TrimEnd('/');
            return trimmedPrefix;
        }
        return null;
    }

    private static string? CombinePrefixes(string? shellPathPrefix, string? routePrefix)
    {
        if (string.IsNullOrWhiteSpace(shellPathPrefix) && string.IsNullOrWhiteSpace(routePrefix))
            return null;
        if (string.IsNullOrWhiteSpace(shellPathPrefix))
            return "/" + routePrefix;
        if (string.IsNullOrWhiteSpace(routePrefix))
            return shellPathPrefix;
        return $"{shellPathPrefix}/{routePrefix}";
    }
}
