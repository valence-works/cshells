namespace CShells.Lifecycle;

/// <summary>Overall outcome of a drain operation.</summary>
public enum DrainStatus
{
    /// <summary>Drain is in progress.</summary>
    Pending,

    /// <summary>All handlers completed within the deadline.</summary>
    Completed,

    /// <summary>The deadline elapsed; handlers were cancelled.</summary>
    TimedOut,

    /// <summary><see cref="IDrainOperation.ForceAsync"/> was called.</summary>
    Forced,
}

/// <summary>Structured result returned by <see cref="IDrainOperation.WaitAsync"/>.</summary>
/// <param name="Shell">The drained shell's descriptor.</param>
/// <param name="Status">Overall outcome.</param>
/// <param name="ScopeWaitElapsed">How long drain phase 1 (scope wait) took.</param>
/// <param name="AbandonedScopeCount">
/// Scope handles still outstanding when phase 1 ended (non-zero only when the phase was bounded out by the deadline).
/// </param>
/// <param name="HandlerResults">One entry per registered <see cref="IDrainHandler"/>.</param>
/// <param name="TerminatorResults">
/// One entry per resolved <see cref="IShellTerminator"/>; empty when termination was skipped
/// (no terminators, ordering-plan failure, or provider already disposed).
/// </param>
public sealed record DrainResult(
    ShellDescriptor Shell,
    DrainStatus Status,
    TimeSpan ScopeWaitElapsed,
    int AbandonedScopeCount,
    IReadOnlyList<DrainHandlerResult> HandlerResults,
    IReadOnlyList<ShellTerminatorResult> TerminatorResults);
