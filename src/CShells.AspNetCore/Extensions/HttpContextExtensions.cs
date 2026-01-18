using CShells.Hosting;
using CShells.Resolution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.AspNetCore.Extensions;

/// <summary>
/// Extension methods for converting <see cref="HttpContext"/> to <see cref="ShellResolutionContext"/>.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Converts an <see cref="HttpContext"/> to a <see cref="ShellResolutionContext"/>.
    /// </summary>
    /// <param name="httpContext">The HTTP context to convert.</param>
    /// <param name="shellHost">The shell host to use for resolution.</param>
    /// <returns>A <see cref="ShellResolutionContext"/> populated with HTTP request data.</returns>
    public static ShellResolutionContext ToShellResolutionContext(this HttpContext httpContext, IShellHost shellHost)
    {
        Guard.Against.Null(httpContext);
        Guard.Against.Null(shellHost);

        var context = new ShellResolutionContext
        {
            ShellHost = shellHost
        };
        
        // Populate common context keys
        context.Set(ShellResolutionContextKeys.Path, httpContext.Request.Path.Value ?? string.Empty);
        context.Set(ShellResolutionContextKeys.Host, httpContext.Request.Host.Host);
        
        // Convert headers to a dictionary
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in httpContext.Request.Headers)
        {
            headers[header.Key] = header.Value.ToString();
        }
        context.Set(ShellResolutionContextKeys.Headers, headers);
        
        // Convert query parameters to a dictionary
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var param in httpContext.Request.Query)
        {
            parameters[param.Key] = param.Value.ToString();
        }
        context.Set(ShellResolutionContextKeys.Parameters, parameters);
        
        // Set user if authenticated
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            context.Set(ShellResolutionContextKeys.User, httpContext.User);
        }
        
        // Set IP address
        if (httpContext.Connection.RemoteIpAddress != null)
        {
            context.Set(ShellResolutionContextKeys.IpAddress, httpContext.Connection.RemoteIpAddress.ToString());
        }
        
        // Store the raw HttpContext for protocol-specific resolvers
        context.Set(ShellResolutionContextKeys.ProtocolContext, httpContext);

        return context;
    }
}
