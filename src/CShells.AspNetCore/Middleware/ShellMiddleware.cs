using CShells.AspNetCore.Extensions;
using CShells.AspNetCore.Routing;
using CShells.Lifecycle;
using CShells.Resolution;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CShells.AspNetCore.Middleware;

/// <summary>
/// Middleware that resolves the current shell from the request and sets
/// <see cref="HttpContext.RequestServices"/> to a scope built from that shell's provider.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="IShell.BeginScope"/> so the shell's active-scope counter stays accurate for
/// drain coordination: when a reload triggers a drain on the outgoing generation, the scope
/// held by this request keeps the old provider alive until the response has been written.
/// </para>
/// </remarks>
public class ShellMiddleware(
    RequestDelegate next,
    IShellResolver resolver,
    IShellRegistry registry,
    DynamicShellEndpointDataSource endpointDataSource,
    ShellMiddlewarePipelineRegistry pipelineRegistry,
    IMemoryCache cache,
    IOptions<ShellMiddlewareOptions> options,
    ILogger<ShellMiddleware>? logger = null)
{
    private readonly RequestDelegate _next = Guard.Against.Null(next);
    private readonly IShellResolver _resolver = Guard.Against.Null(resolver);
    private readonly IShellRegistry _registry = Guard.Against.Null(registry);
    private readonly DynamicShellEndpointDataSource _endpointDataSource = Guard.Against.Null(endpointDataSource);
    private readonly ShellMiddlewarePipelineRegistry _pipelineRegistry = Guard.Against.Null(pipelineRegistry);
    private readonly IMemoryCache _cache = Guard.Against.Null(cache);
    private readonly ShellMiddlewareOptions _options = Guard.Against.Null(options).Value;
    private readonly ILogger<ShellMiddleware> _logger = logger ?? NullLogger<ShellMiddleware>.Instance;

    /// <summary>Invokes the middleware for the current request.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var resolutionContext = context.ToShellResolutionContext();

        // Prefer the shell generation that owns the matched endpoint (set by UseRouting before
        // this middleware). A reload may already have promoted a newer generation by the time
        // this request enters middleware, so resolving only by shell name would route the request
        // into the wrong provider. The endpoint generation is the binding authority.
        var endpointMetadata = context.GetEndpoint()?.Metadata.GetMetadata<ShellEndpointMetadata>();
        ShellId? shellId = endpointMetadata?.ShellId
            ?? await ResolveShellWithCacheAsync(context, resolutionContext, context.RequestAborted).ConfigureAwait(false);

        if (shellId is null)
        {
            _logger.LogDebug("No shell resolved for request, continuing without shell scope");
            await _next(context);
            return;
        }

        _logger.LogInformation("Resolved shell '{ShellId}' for request path '{Path}'", shellId.Value, context.Request.Path);

        IShell shell;
        if (endpointMetadata is not null)
        {
            var matchedShell = _registry.GetAll(endpointMetadata.ShellId.Name)
                .FirstOrDefault(candidate => candidate.Descriptor.Generation == endpointMetadata.Generation);
            if (matchedShell is null)
            {
                _logger.LogWarning(
                    "Matched endpoint for shell '{ShellId}' generation {Generation}, but that exact generation is no longer available.",
                    endpointMetadata.ShellId, endpointMetadata.Generation);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }

            shell = matchedShell;
        }
        else
        {
            // Check whether the shell is already active so we can detect cold activation below.
            var wasCold = _registry.GetActive(shellId.Value.Name) is null;
            try
            {
                // Lazy activation: GetOrActivateAsync returns the active generation if already live,
                // otherwise looks up the blueprint via the provider, builds the shell, and publishes it.
                // Stampede-safe — the per-name semaphore serializes concurrent cold-shell requests.
                shell = await _registry.GetOrActivateAsync(shellId.Value.Name, context.RequestAborted);
            }
            catch (ShellBlueprintNotFoundException)
            {
                _logger.LogInformation("No blueprint registered for shell '{ShellId}'; returning 404.", shellId.Value);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            catch (ShellBlueprintUnavailableException ex)
            {
                _logger.LogWarning(ex, "Blueprint provider unavailable for shell '{ShellId}'; returning 503.", shellId.Value);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }
            catch (ShellGenerationActivationException ex)
            {
                _logger.LogWarning(ex, "Shell generation publication failed for '{ShellId}'; returning 503.", shellId.Value);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }

            // Cold activation: UseRouting() ran before this middleware and either found no endpoint,
            // or selected a host fallback endpoint because shell endpoints had not been registered yet.
            // Now that activation has completed and the ShellEndpointRegistrationHandler has published
            // the shell's endpoints, re-match the request so downstream endpoint middleware can execute
            // the shell handler instead of a preselected fallback.
            if (wasCold && ShouldTryMatchEndpointAfterColdActivation(context.GetEndpoint()))
                TryMatchEndpointAfterColdActivation(context, shellId.Value);
        }

        // Resolve the shell's composed middleware pipeline BEFORE taking the scope. Ordering
        // matters: pipeline entries are removed when a generation reaches Disposed (bounded or
        // forced drains can dispose a generation while requests are still arriving), and
        // BeginScope rejects a Disposed generation — so resolving first means this request
        // either holds the pipeline delegate (which stays usable even if the entry is removed
        // mid-request) or fails loudly at BeginScope. It can never silently run without the
        // shell's middleware.
        var pipeline = _pipelineRegistry.Get(shellId.Value, shell.Descriptor.Generation, _next);

        // BeginScope increments the shell's active-scope counter (so in-flight drains' phase-1
        // waits for this request to complete). The scope is released via OnCompleted (not
        // `await using`) so upstream middleware can still read RequestServices during its
        // post-_next processing — releasing at InvokeAsync return could dispose the DI scope
        // out from under that post-processing and cause ObjectDisposedException.
        var scope = shell.BeginScope();
        context.RequestServices = scope.ServiceProvider;
        context.Response.OnCompleted(() => scope.DisposeAsync().AsTask());

        await (pipeline ?? _next)(context);
    }

    private async ValueTask<ShellId?> ResolveShellWithCacheAsync(
        HttpContext context,
        ShellResolutionContext resolutionContext,
        CancellationToken cancellationToken)
    {
        if (!_options.EnableCaching)
            return await _resolver.ResolveAsync(resolutionContext, cancellationToken).ConfigureAwait(false);

        var cacheKey = BuildCacheKey(context);

        if (_cache.TryGetValue<ShellId?>(cacheKey, out var cached))
            return cached;

        var resolvedShellId = await _resolver.ResolveAsync(resolutionContext, cancellationToken).ConfigureAwait(false);

        using var entry = _cache.CreateEntry(cacheKey);
        entry.Value = resolvedShellId;

        if (resolvedShellId is null)
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(1);
        }
        else
        {
            entry.SlidingExpiration = _options.CacheSlidingExpiration;
            entry.AbsoluteExpirationRelativeToNow = _options.CacheAbsoluteExpiration;
            entry.Size = 1;
            _logger.LogDebug("Cached shell '{ShellId}' for key '{CacheKey}'", resolvedShellId.Value, cacheKey);
        }

        return resolvedShellId;
    }

    private static string BuildCacheKey(HttpContext context)
    {
        var request = context.Request;
        return $"{request.Host}:{request.Path}:{request.Method}";
    }

    private static bool ShouldTryMatchEndpointAfterColdActivation(Endpoint? endpoint)
    {
        if (endpoint is null)
            return true;

        if (endpoint.Metadata.GetMetadata<ShellEndpointMetadata>() is not null)
            return false;

        return endpoint is RouteEndpoint { Order: int.MaxValue };
    }

    /// <summary>
    /// After a cold shell activation, UseRouting() has already run and may have found no endpoint
    /// or selected a host fallback endpoint. The shell's endpoints are now registered in the
    /// <see cref="DynamicShellEndpointDataSource"/>. Walk the data source and set the first
    /// matching endpoint on the context so the downstream endpoint middleware can execute it.
    /// </summary>
    private void TryMatchEndpointAfterColdActivation(HttpContext context, ShellId shellId)
    {
        foreach (var endpoint in _endpointDataSource.Endpoints)
        {
            if (endpoint is not RouteEndpoint routeEndpoint)
                continue;

            var metadata = routeEndpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
            if (metadata is null || !metadata.ShellId.Equals(shellId))
                continue;

            // Check HTTP method constraint first (cheap).
            var methodMetadata = routeEndpoint.Metadata.GetMetadata<HttpMethodMetadata>();
            if (methodMetadata is not null &&
                !methodMetadata.HttpMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
                continue;

            var template = TemplateParser.Parse(routeEndpoint.RoutePattern.RawText ?? "");
            var defaults = new RouteValueDictionary();
            foreach (var kvp in routeEndpoint.RoutePattern.Defaults)
                defaults[kvp.Key] = kvp.Value;

            var matcher = new TemplateMatcher(template, defaults);
            var routeValues = new RouteValueDictionary();

            if (!matcher.TryMatch(context.Request.Path, routeValues))
                continue;

            // TemplateMatcher only matches structurally; it does NOT evaluate inline route
            // constraints (`:int`, `:guid`, custom). Without this step, an endpoint like
            // `{tenant}/orders/{id:int}` would be selected for `/acme/orders/hello`, sending
            // the request to the wrong handler. Apply the framework's resolved IRouteConstraint
            // instances so cold-start re-matching honours the same constraints UseRouting does.
            if (!ApplyInlineConstraints(routeEndpoint.RoutePattern, routeValues, context))
                continue;

            context.SetEndpoint(routeEndpoint);
            context.Request.RouteValues = routeValues;
            _logger.LogDebug(
                "Cold-start re-matched endpoint '{Pattern}' for shell '{Shell}'",
                routeEndpoint.RoutePattern.RawText, shellId);
            return;
        }
    }

    private static bool ApplyInlineConstraints(
        RoutePattern pattern,
        RouteValueDictionary routeValues,
        HttpContext context)
    {
        if (pattern.ParameterPolicies.Count == 0)
            return true;

        // Lazily resolved on first non-pre-built policy reference.
        IInlineConstraintResolver? resolver = null;

        foreach (var (parameterName, policies) in pattern.ParameterPolicies)
        {
            foreach (var policyRef in policies)
            {
                // RoutePattern resolves inline policies at build time and stores them on
                // the reference. Fall back to runtime resolution for patterns built without
                // an IInlineConstraintResolver in scope.
                var policy = policyRef.ParameterPolicy;
                if (policy is null && policyRef.Content is { Length: > 0 } content)
                {
                    resolver ??= context.RequestServices.GetService<IInlineConstraintResolver>();
                    policy = resolver?.ResolveConstraint(content);
                }

                if (policy is IRouteConstraint constraint
                    && !constraint.Match(context, route: null, parameterName, routeValues, RouteDirection.IncomingRequest))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
