using CShells.AspNetCore.Extensions;
using CShells.Hosting;
using CShells.Resolution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CShells.AspNetCore.Middleware;

/// <summary>
/// Middleware that resolves the current shell from the request and sets the appropriate
/// <see cref="HttpContext.RequestServices"/> scope for the duration of the request.
/// </summary>
public class ShellMiddleware(
    RequestDelegate next,
    IShellResolver resolver,
    IShellHost host,
    IMemoryCache cache,
    IOptions<ShellMiddlewareOptions> options,
    ILogger<ShellMiddleware>? logger = null)
{
    private readonly RequestDelegate _next = Guard.Against.Null(next);
    private readonly IShellResolver _resolver = Guard.Against.Null(resolver);
    private readonly IShellHost _host = Guard.Against.Null(host);
    private readonly IMemoryCache _cache = Guard.Against.Null(cache);
    private readonly ShellMiddlewareOptions _options = Guard.Against.Null(options).Value;
    private readonly ILogger<ShellMiddleware> _logger = logger ?? NullLogger<ShellMiddleware>.Instance;

    /// <summary>
    /// Invokes the middleware for the current request.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var resolutionContext = context.ToShellResolutionContext(_host);
        var shellHost = resolutionContext.ShellHost;
        if(shellHost.AllShells.Count == 0)
        {
            _logger.LogDebug("No shells registered, continuing without shell scope");
            await _next(context);
            return;
        }

        var shellId = ResolveShellWithCache(context, resolutionContext);

        if (shellId is null)
        {
            _logger.LogDebug("No shell resolved for request, continuing without shell scope");
            await _next(context);
            return;
        }

        _logger.LogInformation("Resolved shell '{ShellId}' for request path '{Path}'", shellId.Value, context.Request.Path);

        var shellContext = _host.GetShell(shellId.Value);

        var scope = shellContext.ServiceProvider.CreateScope();
        context.RequestServices = scope.ServiceProvider;

        // Register the scope for disposal when the request completes
        // This ensures the scope lives for the entire request, including endpoint execution
        context.Response.RegisterForDispose(scope);

        await _next(context);
    }

    private ShellId? ResolveShellWithCache(HttpContext context, ShellResolutionContext resolutionContext)
    {
        if (!_options.EnableCaching)
            return _resolver.Resolve(resolutionContext);

        var cacheKey = BuildCacheKey(context);

        var shellId = _cache.GetOrCreate(cacheKey, entry =>
        {
            var resolvedShellId = _resolver.Resolve(resolutionContext);

            if (resolvedShellId is null)
            {
                // Don't cache null results - set very short expiration
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
        });

        return shellId;
    }

    private string BuildCacheKey(HttpContext context)
    {
        var request = context.Request;
        return $"{request.Host}:{request.Path}:{request.Method}";
    }
}
