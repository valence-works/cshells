using CShells.Management.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace CShells.Management.Api;

/// <summary>
/// Extension methods that map the CShells management REST API onto an
/// <see cref="IEndpointRouteBuilder"/>.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the CShells management REST API under <paramref name="prefix"/> and returns the
    /// route group so the host can attach standard ASP.NET Core endpoint conventions
    /// (<c>RequireAuthorization</c>, <c>RequireCors</c>, <c>RequireRateLimiting</c>,
    /// <c>WithTags</c>, <c>WithOpenApi</c>, <c>AddEndpointFilter</c>, …).
    /// </summary>
    /// <param name="endpoints">The route builder to map onto.</param>
    /// <param name="prefix">Route prefix; defaults to <c>/_admin/shells</c>.</param>
    /// <returns>The created <see cref="RouteGroupBuilder"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>The endpoints are unprotected by default.</b> The package applies no authorization,
    /// authentication, or rate-limit policy of its own — it is the host's responsibility to
    /// chain such conventions on the returned builder before exposing the endpoints to any
    /// non-localhost interface.
    /// </para>
    /// <para>
    /// <b>The blueprint endpoints expose registered <c>ConfigurationData</c> verbatim.</b>
    /// Configuration values may contain host-controlled secrets (connection strings, API
    /// keys, etc.). Production-style deployments MUST chain
    /// <see cref="AuthorizationEndpointConventionBuilderExtensions.RequireAuthorization{TBuilder}(TBuilder)"/>
    /// (or an equivalent gate) on the returned <see cref="RouteGroupBuilder"/>.
    /// </para>
    /// <para>
    /// The package contributes no service registrations. <c>IShellRegistry</c> is resolved
    /// from the request services (root scope, since management endpoints sit outside any
    /// shell's prefix). Hosts must register CShells (e.g., <c>AddCShells</c>) before mapping
    /// these endpoints.
    /// </para>
    /// </remarks>
    public static RouteGroupBuilder MapShellManagementApi(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/_admin/shells")
    {
        Guard.Against.Null(endpoints);
        Guard.Against.NullOrWhiteSpace(prefix);

        var group = endpoints.MapGroup(prefix);
        ListShellsHandler.Map(group);
        GetShellHandler.Map(group);
        GetBlueprintHandler.Map(group);
        ReloadShellHandler.Map(group);
        ReloadAllHandler.Map(group);
        ForceDrainHandler.Map(group);
        return group;
    }
}
