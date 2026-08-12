namespace CShells.Management.Api.Models;

/// <summary>
/// Per-generation drain outcome returned in the array body of <c>POST /{name}/force-drain</c>.
/// One entry per forced generation.
/// </summary>
internal sealed record DrainResultResponse(
    string Name,
    int Generation,
    string Status,
    TimeSpan ScopeWaitElapsed,
    int AbandonedScopeCount,
    IReadOnlyList<DrainHandlerResultResponse> HandlerResults,
    IReadOnlyList<ShellTerminatorResultResponse> TerminatorResults);

/// <summary>One drain-handler outcome inside a <see cref="DrainResultResponse"/>.</summary>
internal sealed record DrainHandlerResultResponse(
    string HandlerType,
    string Outcome,
    TimeSpan Elapsed,
    string? ErrorMessage);

/// <summary>One shell-terminator outcome inside a <see cref="DrainResultResponse"/>.</summary>
internal sealed record ShellTerminatorResultResponse(
    string TerminatorType,
    string Outcome,
    TimeSpan Elapsed,
    string? ErrorMessage);
