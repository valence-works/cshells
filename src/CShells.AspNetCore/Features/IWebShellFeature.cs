using CShells.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace CShells.AspNetCore.Features;

/// <summary>
/// Extends <see cref="IShellFeature"/> with ASP.NET Core endpoint mapping.
/// </summary>
/// <remarks>
/// Web shell features use endpoint routing to register routes dynamically.
/// This allows shells to be loaded, modified, or removed at runtime without
/// requiring application restart or middleware pipeline reconfiguration.
/// </remarks>
public interface IWebShellFeature : IShellFeature
{
    /// <summary>
    /// Maps endpoints for this feature within the shell's route scope.
    /// </summary>
    /// <param name="endpoints">
    /// The endpoint route builder. Endpoints registered here will be scoped to the shell
    /// and executed within the shell's service provider context.
    /// </param>
    /// <param name="environment">The hosting environment, or null if not registered in the service provider.</param>
    /// <remarks>
    /// <para>
    /// This method is called when a shell is activated, either during application startup
    /// or when a shell is dynamically added at runtime.
    /// </para>
    /// <para>
    /// All routes registered here will be automatically prefixed with the shell's path
    /// (if configured via shell properties) and will execute within the shell's isolated
    /// service provider scope.
    /// </para>
    /// </remarks>
    void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment);
}
