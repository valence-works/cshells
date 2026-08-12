using CShells.Lifecycle;
using CShells.Management.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CShells.Management.Api.Endpoints;

/// <summary>Handler for <c>GET /{name}</c> — focused view of one shell.</summary>
internal static class GetShellHandler
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder group) =>
        group.MapGet("/{name}", HandleAsync).WithName("GetShell");

    private static async Task<IResult> HandleAsync(
        string name,
        IShellRegistry registry,
        HttpContext ctx,
        CancellationToken ct)
    {
        try
        {
            // Blueprint fetch is best-effort — a transiently-unavailable blueprint store must
            // not silence live-generation data on a monitoring endpoint. Matches the
            // partial-failure-tolerance pattern in ListShellsHandler. Cancellation
            // (host shutdown) is allowed to propagate to the outer catch.
            ProvidedBlueprint? blueprint = null;
            try
            {
                blueprint = await registry.GetBlueprintAsync(name, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Surface as null blueprint; the response still carries live generations.
            }

            // Per FR-011, surface every generation NOT yet disposed. The registry releases a generation
            // once it reaches Disposed, so GetAll already excludes torn-down generations; the Disposed
            // filter is a defensive guard against a generation that transitions to Disposed between the
            // GetAll snapshot and this projection.
            var liveGenerations = registry.GetAll(name)
                .Where(s => s.State != ShellLifecycleState.Disposed)
                .ToArray();

            if (blueprint is null && liveGenerations.Length == 0)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Shell '{name}' has no blueprint and no live generations.",
                    instance: ctx.Request.Path);
            }

            var blueprintDto = await DtoMappers.MapBlueprintAsync(blueprint, ct);
            var generationsDto = liveGenerations.Select(DtoMappers.MapGeneration).ToArray();

            return Results.Ok(new ShellDetailResponse(name, blueprintDto, generationsDto));
        }
        catch (Exception ex)
        {
            return ResultMapper.MapException(ex, ctx);
        }
    }
}
