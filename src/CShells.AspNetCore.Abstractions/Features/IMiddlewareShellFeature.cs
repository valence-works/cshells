using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace CShells.AspNetCore.Features;

/// <summary>
/// Extends <see cref="IShellFeature"/> with ASP.NET Core middleware registration.
/// </summary>
/// <remarks>
/// <para>
/// Middleware shell features allow shell features to register middleware components
/// into the ASP.NET Core request pipeline. Unlike <see cref="IWebShellFeature"/> which
/// registers endpoints, this interface registers middleware that runs before endpoint dispatch.
/// </para>
/// <para>
/// Middleware registered through this interface runs only for requests resolved to the
/// feature's shell, composed into a per-shell pipeline that executes at the point where
/// <c>MapShells()</c> was called — immediately after the shell resolution middleware has set
/// <c>HttpContext.RequestServices</c> to a scope of the shell's service provider. If the shell
/// has a path prefix configured (<c>WebRouting:Path</c>), the middleware additionally only runs
/// for requests under that prefix and observes the prefix-stripped <c>PathBase</c>/<c>Path</c>;
/// the prefix is re-applied before the rest of the host pipeline (including endpoint execution)
/// continues, so path rewrites made by the middleware are preserved downstream.
/// </para>
/// </remarks>
public interface IMiddlewareShellFeature : IShellFeature
{
    /// <summary>
    /// Relative order in which this feature's middleware is applied within the shell's pipeline.
    /// Lower values run earlier (outermost). Features with equal order are applied in
    /// feature-dependency (discovery) order. Defaults to 0.
    /// </summary>
    int Order => 0;

    /// <summary>
    /// Registers middleware components for this feature within the shell's pipeline scope.
    /// </summary>
    /// <param name="app">
    /// The application builder for the shell's pipeline branch. Middleware registered here is
    /// scoped to the shell's path prefix (if configured) and executes within the shell's
    /// service provider context.
    /// </param>
    /// <param name="environment">The hosting environment, or null if not registered in the service provider.</param>
    /// <remarks>
    /// <para>
    /// This method is called when the shell is activated — during application startup, on lazy
    /// (cold) activation by the first matching request, on dynamic activation at runtime, and
    /// again for each new generation when the shell is reloaded. Shells activated before
    /// <c>MapShells()</c> runs are registered retroactively when <c>MapShells()</c> is called.
    /// </para>
    /// <para>
    /// Middleware registered here runs after the shell resolution middleware has set
    /// <c>HttpContext.RequestServices</c> to the shell's service provider, so any
    /// services resolved from the request will come from the correct shell scope.
    /// Middleware types implementing <c>IMiddleware</c> are supported: CShells guarantees an
    /// <c>IMiddlewareFactory</c> is available in the shell container.
    /// </para>
    /// </remarks>
    void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment);
}

