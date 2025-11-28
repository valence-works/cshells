using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.AspNetCore;

/// <summary>
/// Middleware that resolves the current shell from the request and sets the appropriate
/// <see cref="HttpContext.RequestServices"/> scope for the duration of the request.
/// </summary>
public class ShellMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IShellResolver _resolver;
    private readonly IShellHost _host;
    private readonly ILogger<ShellMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="resolver">The shell resolver to determine the shell for each request.</param>
    /// <param name="host">The shell host containing available shells.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public ShellMiddleware(
        RequestDelegate next,
        IShellResolver resolver,
        IShellHost host,
        ILogger<ShellMiddleware>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(host);

        _next = next;
        _resolver = resolver;
        _host = host;
        _logger = logger ?? NullLogger<ShellMiddleware>.Instance;
    }

    /// <summary>
    /// Invokes the middleware for the current request.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var shellId = _resolver.Resolve(context);

        if (shellId is null)
        {
            _logger.LogDebug("No shell resolved for request, continuing without shell scope");
            await _next(context);
            return;
        }

        _logger.LogDebug("Resolved shell '{ShellId}' for request", shellId.Value);

        var shellContext = _host.GetShell(shellId.Value);
        var scope = shellContext.ServiceProvider.CreateScope();
        var originalRequestServices = context.RequestServices;

        try
        {
            context.RequestServices = scope.ServiceProvider;
            await _next(context);
        }
        finally
        {
            context.RequestServices = originalRequestServices;
            scope.Dispose();
        }
    }
}
