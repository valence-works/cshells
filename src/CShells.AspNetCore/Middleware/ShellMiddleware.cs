using CShells.AspNetCore.Extensions;
using CShells.Hosting;
using CShells.Resolution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.AspNetCore.Middleware;

/// <summary>
/// Middleware that resolves the current shell from the request and sets the appropriate
/// <see cref="HttpContext.RequestServices"/> scope for the duration of the request.
/// </summary>
public class ShellMiddleware(
    RequestDelegate next,
    IShellResolver resolver,
    IShellHost host,
    ILogger<ShellMiddleware>? logger = null)
{
    private readonly RequestDelegate _next = Guard.Against.Null(next);
    private readonly IShellResolver _resolver = Guard.Against.Null(resolver);
    private readonly IShellHost _host = Guard.Against.Null(host);
    private readonly ILogger<ShellMiddleware> _logger = logger ?? NullLogger<ShellMiddleware>.Instance;

    /// <summary>
    /// Invokes the middleware for the current request.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var resolutionContext = context.ToShellResolutionContext();
        var shellId = _resolver.Resolve(resolutionContext);

        if (shellId is null)
        {
            _logger.LogDebug("No shell resolved for request, continuing without shell scope");
            await _next(context);
            return;
        }

        _logger.LogInformation("Resolved shell '{ShellId}' for request path '{Path}'", shellId.Value, context.Request.Path);

        var shellContext = _host.GetShell(shellId.Value);
        var originalRequestServices = context.RequestServices;

        using var scope = shellContext.ServiceProvider.CreateScope();
        try
        {
            context.RequestServices = scope.ServiceProvider;
            await _next(context);
        }
        finally
        {
            context.RequestServices = originalRequestServices;
        }
    }
}
