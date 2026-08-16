using CShells.AspNetCore.Features;
using CShells.AspNetCore.Middleware;
using CShells.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
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
/// Reacts to shell lifecycle transitions by (re-)registering or removing endpoints in the
/// dynamic endpoint data source and per-shell middleware pipelines in the
/// <see cref="ShellMiddlewarePipelineRegistry"/>. Subscribed to the registry via
/// <see cref="IShellLifecycleSubscriber"/>.
/// </summary>
public sealed class ShellEndpointRegistrationHandler : IShellLifecycleSubscriber
{
    private readonly DynamicShellEndpointDataSource _endpointDataSource;
    private readonly EndpointRouteBuilderAccessor _endpointRouteBuilderAccessor;
    private readonly ApplicationBuilderAccessor _applicationBuilderAccessor;
    private readonly ShellMiddlewarePipelineRegistry _pipelineRegistry;
    private readonly IShellFeatureFactory _featureFactory;
    private readonly IHostEnvironment? _environment;
    private readonly ILogger<ShellEndpointRegistrationHandler> _logger;

    public ShellEndpointRegistrationHandler(
        DynamicShellEndpointDataSource endpointDataSource,
        IShellFeatureFactory featureFactory,
        EndpointRouteBuilderAccessor endpointRouteBuilderAccessor,
        ApplicationBuilderAccessor applicationBuilderAccessor,
        ShellMiddlewarePipelineRegistry pipelineRegistry,
        IHostEnvironment? environment = null,
        ILogger<ShellEndpointRegistrationHandler>? logger = null)
    {
        _endpointDataSource = Guard.Against.Null(endpointDataSource);
        _endpointRouteBuilderAccessor = Guard.Against.Null(endpointRouteBuilderAccessor);
        _applicationBuilderAccessor = Guard.Against.Null(applicationBuilderAccessor);
        _pipelineRegistry = Guard.Against.Null(pipelineRegistry);
        _featureFactory = Guard.Against.Null(featureFactory);
        _environment = environment;
        _logger = logger ?? NullLogger<ShellEndpointRegistrationHandler>.Instance;
    }

    /// <inheritdoc />
    public Task OnStateChangedAsync(IShell shell, ShellLifecycleState previous, ShellLifecycleState current, CancellationToken cancellationToken = default)
    {
        // Register when a shell becomes Active, tear down when it starts deactivating or draining.
        if (previous == ShellLifecycleState.Initializing && current == ShellLifecycleState.Active)
        {
            if (_endpointRouteBuilderAccessor.EndpointRouteBuilder is null)
            {
                _logger.LogWarning(
                    "Cannot register endpoints or middleware for shell '{Shell}': MapShells() has not run yet. " +
                    "Registration is replayed when MapShells() captures the routing infrastructure.",
                    shell.Descriptor);
                return Task.CompletedTask;
            }

            try
            {
                RegisterActiveShell(shell);
            }
            catch (Exception ex)
            {
                // Endpoint publication is part of activation's candidate boundary. The registry
                // treats this typed failure as a candidate rejection, disposes the new provider,
                // and preserves the previous active generation.
                throw new ShellGenerationActivationException(shell.Descriptor, ex);
            }
            return Task.CompletedTask;
        }

        if (current == ShellLifecycleState.Deactivating ||
            current == ShellLifecycleState.Draining ||
            current == ShellLifecycleState.Disposed)
        {
            _logger.LogInformation("Removing endpoints for shell '{Shell}' generation {Generation} ({State})",
                shell.Descriptor, shell.Descriptor.Generation, current);
            _endpointDataSource.RemoveEndpoints(new ShellId(shell.Descriptor.Name), shell.Descriptor.Generation);

            // The middleware pipeline is removed only on disposal: unlike endpoints (which must
            // stop matching as soon as the generation deactivates), the pipeline is only looked
            // up by requests that already hold this generation's scope — and drain's scope-wait
            // keeps the generation from reaching Disposed while any such request is in flight.
            if (current == ShellLifecycleState.Disposed)
                _pipelineRegistry.Remove(new ShellId(shell.Descriptor.Name), shell.Descriptor.Generation);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers endpoints and the middleware pipeline for an active shell. Invoked on the
    /// Initializing → Active transition, and replayed by <c>MapShells()</c> for shells that
    /// activated before the routing infrastructure was captured.
    /// </summary>
    public void RegisterActiveShell(IShell shell)
    {
        Guard.Against.Null(shell);
        _logger.LogInformation("Registering endpoints for active shell '{Shell}'", shell.Descriptor);
        RegisterShellEndpoints(shell);
    }

    private void RegisterShellEndpoints(IShell shell)
    {
        var endpointRouteBuilder = _endpointRouteBuilderAccessor.EndpointRouteBuilder;
        if (endpointRouteBuilder is null)
            return;

        // Refresh the standard host endpoint inventory immediately before mapping so candidate
        // validation sees the same routes that ASP.NET Core will see after publication.
        _endpointDataSource.SetHostEndpoints(
            endpointRouteBuilder.DataSources
                .SelectMany(dataSource => dataSource.Endpoints)
                .Where(endpoint => endpoint.Metadata.GetMetadata<ShellEndpointMetadata>() is null));

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

        PipelineRegistration? pipelineRegistration;
        try
        {
            pipelineRegistration = RegisterShellMiddleware(settings, shell, allFeatureDescriptors, featureContext, shellPathPrefix);
        }
        catch (Exception ex)
        {
            // Middleware composition is staged with the endpoint candidate. Preserve the existing
            // fail-closed behavior without publishing a pipeline until route validation succeeds.
            _logger.LogError(ex,
                "Failed to compose the middleware pipeline for shell '{Shell}' generation {Generation}. " +
                "The shell will respond 503 to all requests until it is successfully reloaded.",
                shell.Descriptor, shell.Descriptor.Generation);

            pipelineRegistration = new PipelineRegistration(
                context =>
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    return Task.CompletedTask;
                },
                new ShellPipelineContinuation());
        }

        var webFeatures = DiscoverWebFeatures(settings, allFeatureDescriptors);
        foreach (var (featureId, featureType) in webFeatures)
        {
            try
            {
                var feature = _featureFactory.CreateFeature<IWebShellFeature>(featureType, settings, featureContext);
                var beforeMapping = shellEndpointBuilder.GetRawEndpoints().ToHashSet();
                feature.MapEndpoints(shellEndpointBuilder, _environment);
                var mappedEndpoints = shellEndpointBuilder.GetRawEndpoints()
                    .Where(endpoint => !beforeMapping.Contains(endpoint))
                    .ToList();
                shellEndpointBuilder.AssignFeature(featureId, mappedEndpoints);

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

        // Validate the complete candidate before replacing this shell's published snapshot.
        // A conflict leaves the old generation available to both routing and in-flight calls.
        _endpointDataSource.PublishGeneration(settings.Id, shell.Descriptor.Generation, shellEndpoints);

        // Commit the staged middleware only after publication succeeds, avoiding a half-built
        // pipeline when feature mapping or validation fails.
        if (pipelineRegistration is not null)
            _pipelineRegistry.Set(settings.Id, shell.Descriptor.Generation,
                pipelineRegistration.Pipeline, pipelineRegistration.Continuation);

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

    private PipelineRegistration? RegisterShellMiddleware(
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
            return null;
        }

        var middlewareFeatures = new List<(string FeatureId, IMiddlewareShellFeature Feature)>();
        foreach (var (featureId, featureType) in DiscoverMiddlewareFeatures(settings, allFeatureDescriptors))
        {
            try
            {
                middlewareFeatures.Add((featureId, _featureFactory.CreateFeature<IMiddlewareShellFeature>(featureType, settings, featureContext)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create middleware feature '{FeatureId}' for shell '{Shell}'", featureId, shell.Descriptor);
                throw;
            }
        }

        if (middlewareFeatures.Count == 0)
            return null;

        _logger.LogInformation("Registering middleware for {Count} feature(s) in shell '{Shell}'",
            middlewareFeatures.Count, shell.Descriptor);

        // Stable sort: equal Order preserves feature-dependency (discovery) order.
        var orderedFeatures = middlewareFeatures.OrderBy(f => f.Feature.Order).ToList();

        // Compose a per-shell pipeline on a clone of the builder captured at MapShells() time.
        // Cloning keeps ServerFeatures available while leaving the built root pipeline untouched;
        // the shell's own provider becomes ApplicationServices so build-time middleware
        // construction resolves against the shell container.
        var builder = appBuilder.New();
        builder.ApplicationServices = shell.ServiceProvider;

        // The terminal rejoins the host pipeline through the continuation bound by the registry
        // on first Get. It must not blanket-reset the request path: deliberate Path rewrites made
        // by feature middleware belong to the request and flow downstream, exactly as they would
        // for middleware in the host pipeline.
        var continuation = new ShellPipelineContinuation();

        void ApplyFeatures(IApplicationBuilder target)
        {
            foreach (var (featureId, feature) in orderedFeatures)
            {
                try
                {
                    feature.UseMiddleware(target, _environment);
                    _logger.LogDebug("Registered middleware for feature '{FeatureId}' in shell '{Shell}'", featureId, shell.Descriptor);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register middleware for feature '{FeatureId}' in shell '{Shell}'", featureId, shell.Descriptor);
                    throw;
                }
            }
        }

        if (!string.IsNullOrEmpty(shellPathPrefix))
        {
            builder.Map(shellPathPrefix, branch =>
            {
                ApplyFeatures(branch);

                // Undo exactly what Map stripped — re-apply the matched prefix segment (in its
                // original request casing) — while preserving any Path rewrite feature middleware
                // made inside the branch. Downstream host middleware then sees the full path.
                branch.Run(context =>
                {
                    var pathBase = context.Request.PathBase.Value ?? string.Empty;
                    if (pathBase.EndsWith(shellPathPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var matchedSegment = pathBase[^shellPathPrefix.Length..];
                        context.Request.PathBase = new PathString(pathBase[..^shellPathPrefix.Length]);
                        context.Request.Path = new PathString(matchedSegment + context.Request.Path.Value);
                    }
                    return continuation.Next(context);
                });
            });
            builder.Run(context => continuation.Next(context)); // requests outside the prefix skip the features and rejoin
        }
        else
        {
            ApplyFeatures(builder);
            builder.Run(context => continuation.Next(context));
        }

        return new PipelineRegistration(builder.Build(), continuation);
    }

    private sealed record PipelineRegistration(RequestDelegate Pipeline, ShellPipelineContinuation Continuation);

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

        // "/" means root-mounted, i.e. no prefix scoping — and ASP.NET's Map rejects any
        // pathMatch ending in '/', so passing it through would throw during activation.
        return trimmedPath == "/" ? null : trimmedPath;
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
