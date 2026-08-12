using CShells.Lifecycle;
using CShells.Management.Api.Models;

namespace CShells.Management.Api.Endpoints;

/// <summary>
/// Maps registry/lifecycle types to the management API's JSON-friendly response shapes.
/// All mapping methods are pure — they read from the supplied inputs and allocate fresh
/// DTO instances.
/// </summary>
internal static class DtoMappers
{
    public static ShellGenerationResponse MapGeneration(IShell shell) =>
        new(
            Generation: shell.Descriptor.Generation,
            State: shell.State.ToString(),
            CreatedAt: shell.Descriptor.CreatedAt,
            Drain: MapDrain(shell.Drain));

    public static async Task<BlueprintResponse?> MapBlueprintAsync(ProvidedBlueprint? provided, CancellationToken ct)
    {
        if (provided is null)
            return null;

        // ComposeAsync is documented as re-invocable and side-effect-free, so it's safe to
        // call here to materialize features + ConfigurationData for the response.
        var settings = await provided.Blueprint.ComposeAsync(ct).ConfigureAwait(false);
        return new(
            Name: provided.Blueprint.Name,
            Features: settings.EnabledFeatures.ToArray(),
            ConfigurationData: settings.ConfigurationData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
    }

    public static ReloadResultResponse MapReload(ReloadResult result) =>
        new(
            Name: result.Name,
            Success: result.Error is null && result.NewShell is not null,
            NewShell: result.NewShell is null ? null : MapGeneration(result.NewShell),
            Drain: MapDrain(result.Drain),
            Error: result.Error is null
                ? null
                : new ErrorDescription(result.Error.GetType().Name, result.Error.Message));

    public static DrainResultResponse MapDrainResult(DrainResult result) =>
        new(
            Name: result.Shell.Name,
            Generation: result.Shell.Generation,
            Status: result.Status.ToString(),
            ScopeWaitElapsed: result.ScopeWaitElapsed,
            AbandonedScopeCount: result.AbandonedScopeCount,
            HandlerResults: result.HandlerResults.Select(MapHandlerResult).ToArray(),
            TerminatorResults: result.TerminatorResults.Select(MapTerminatorResult).ToArray());

    private static DrainHandlerResultResponse MapHandlerResult(DrainHandlerResult hr) =>
        new(
            HandlerType: hr.HandlerTypeName,
            Outcome: hr.Completed ? "Completed" : (hr.Error is not null ? "Faulted" : "Cancelled"),
            Elapsed: hr.Elapsed,
            ErrorMessage: hr.Error?.Message);

    private static ShellTerminatorResultResponse MapTerminatorResult(ShellTerminatorResult tr) =>
        new(
            TerminatorType: tr.TerminatorTypeName,
            Outcome: tr.Completed ? "Completed" : (tr.Error is not null ? "Faulted" : "Cancelled"),
            Elapsed: tr.Elapsed,
            ErrorMessage: tr.Error?.Message);

    private static DrainSnapshot? MapDrain(IDrainOperation? drain) => drain is null ? null : new DrainSnapshot(drain.Status.ToString(), drain.Deadline);
}
