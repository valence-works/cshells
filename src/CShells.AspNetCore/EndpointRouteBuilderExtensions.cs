using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace CShells.AspNetCore;

/// <summary>
/// Extension methods for <see cref="IEndpointRouteBuilder"/> to support shell-prefixed routing.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <param name="endpoints">The endpoint route builder.</param>
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps a GET endpoint with a shell path prefix parameter.
        /// The route pattern will be "/{shellPath}/..." where shellPath is captured as a route parameter.
        /// </summary>
        /// <param name="pattern">The route pattern after the shell path (e.g., "greet" becomes "/{shellPath}/greet").</param>
        /// <param name="handler">The request handler.</param>
        /// <returns>The route handler builder for further configuration.</returns>
        public RouteHandlerBuilder MapGetWithShellPrefix(string pattern,
            Delegate handler)
        {
            var routePattern = $"{{shellPath}}/{pattern.TrimStart('/')}";
            return endpoints.MapGet(routePattern, handler);
        }

        /// <summary>
        /// Maps a POST endpoint with a shell path prefix parameter.
        /// The route pattern will be "/{shellPath}/..." where shellPath is captured as a route parameter.
        /// </summary>
        /// <param name="pattern">The route pattern after the shell path (e.g., "greet" becomes "/{shellPath}/greet").</param>
        /// <param name="handler">The request handler.</param>
        /// <returns>The route handler builder for further configuration.</returns>
        public RouteHandlerBuilder MapPostWithShellPrefix(string pattern,
            Delegate handler)
        {
            var routePattern = $"{{shellPath}}/{pattern.TrimStart('/')}";
            return endpoints.MapPost(routePattern, handler);
        }

        /// <summary>
        /// Maps a PUT endpoint with a shell path prefix parameter.
        /// The route pattern will be "/{shellPath}/..." where shellPath is captured as a route parameter.
        /// </summary>
        /// <param name="pattern">The route pattern after the shell path (e.g., "greet" becomes "/{shellPath}/greet").</param>
        /// <param name="handler">The request handler.</param>
        /// <returns>The route handler builder for further configuration.</returns>
        public RouteHandlerBuilder MapPutWithShellPrefix(string pattern,
            Delegate handler)
        {
            var routePattern = $"{{shellPath}}/{pattern.TrimStart('/')}";
            return endpoints.MapPut(routePattern, handler);
        }

        /// <summary>
        /// Maps a DELETE endpoint with a shell path prefix parameter.
        /// The route pattern will be "/{shellPath}/..." where shellPath is captured as a route parameter.
        /// </summary>
        /// <param name="pattern">The route pattern after the shell path (e.g., "greet" becomes "/{shellPath}/greet").</param>
        /// <param name="handler">The request handler.</param>
        /// <returns>The route handler builder for further configuration.</returns>
        public RouteHandlerBuilder MapDeleteWithShellPrefix(string pattern,
            Delegate handler)
        {
            var routePattern = $"{{shellPath}}/{pattern.TrimStart('/')}";
            return endpoints.MapDelete(routePattern, handler);
        }

        /// <summary>
        /// Maps a PATCH endpoint with a shell path prefix parameter.
        /// The route pattern will be "/{shellPath}/..." where shellPath is captured as a route parameter.
        /// </summary>
        /// <param name="pattern">The route pattern after the shell path (e.g., "greet" becomes "/{shellPath}/greet").</param>
        /// <param name="handler">The request handler.</param>
        /// <returns>The route handler builder for further configuration.</returns>
        public RouteHandlerBuilder MapPatchWithShellPrefix(string pattern,
            Delegate handler)
        {
            var routePattern = $"{{shellPath}}/{pattern.TrimStart('/')}";
            return endpoints.MapPatch(routePattern, handler);
        }
    }
}
