namespace CShells.Hosting;

/// <summary>Scheduling and terminal status of one target within an activation run.</summary>
public enum ShellActivationTargetStatus
{
    /// <summary>No activation attempt has started.</summary>
    Pending,

    /// <summary>An activation attempt is in progress.</summary>
    Attempting,

    /// <summary>An unsuccessful attempt is awaiting the remaining serial initial pass.</summary>
    RetryPending,

    /// <summary>A retry delay has been armed.</summary>
    RetryScheduled,

    /// <summary>The run verified a settled current shell for this target.</summary>
    Succeeded,

    /// <summary>A settled concrete CShells shell satisfied this target externally.</summary>
    SatisfiedExternally,

    /// <summary>The target ended without success and no retry is scheduled.</summary>
    Failed,

    /// <summary>The run or retry policy stopped scheduling this target.</summary>
    Stopped,

    /// <summary>A retry policy failed or supplied an invalid decision.</summary>
    PolicyError
}

/// <summary>Immutable safe snapshot of the latest activation state for one target.</summary>
/// <remarks>
/// Snapshot state contains no exception object, message, or stack trace. Exception details are
/// available transiently to retry-policy and observer callbacks through <see cref="ShellActivationAttempt"/>.
/// Failure count and first/last failure times are cumulative for this run. Last outcome, attempt times,
/// and error fields describe its latest owned attempt. External satisfaction preserves that attempt history.
/// </remarks>
public sealed record ShellActivationAttemptState
{
    public ShellActivationAttemptState(
        string shellName,
        int attemptCount,
        ShellActivationTargetStatus status,
        int failedAttemptCount = 0,
        DateTimeOffset? firstFailedAt = null,
        DateTimeOffset? lastFailedAt = null,
        TimeSpan? retryDelay = null,
        long? verifiedGeneration = null,
        ShellActivationAttemptOutcome? lastOutcome = null,
        DateTimeOffset? lastAttemptStartedAt = null,
        DateTimeOffset? lastAttemptCompletedAt = null,
        DateTimeOffset? nextAttemptAt = null,
        string? errorCode = null,
        string? errorType = null,
        string? policyErrorCode = null)
    {
        ShellName = shellName;
        AttemptCount = attemptCount;
        Status = status;
        FailedAttemptCount = failedAttemptCount;
        FirstFailedAt = firstFailedAt;
        LastFailedAt = lastFailedAt;
        RetryDelay = retryDelay;
        VerifiedGeneration = verifiedGeneration;
        LastOutcome = lastOutcome;
        LastAttemptStartedAt = lastAttemptStartedAt;
        LastAttemptCompletedAt = lastAttemptCompletedAt;
        NextAttemptAt = nextAttemptAt;
        ErrorCode = errorCode;
        ErrorType = errorType;
        PolicyErrorCode = policyErrorCode;
    }

    /// <summary>The target shell name.</summary>
    public string ShellName { get; init; }

    /// <summary>The number of activation attempts started by this run for the target.</summary>
    public int AttemptCount { get; init; }

    /// <summary>The current scheduling or terminal status.</summary>
    public ShellActivationTargetStatus Status { get; init; }

    /// <summary>Number of completed unsuccessful activation attempts.</summary>
    public int FailedAttemptCount { get; init; }

    /// <summary>Completion time of the first unsuccessful activation attempt.</summary>
    public DateTimeOffset? FirstFailedAt { get; init; }

    /// <summary>Completion time of the most recent unsuccessful activation attempt.</summary>
    public DateTimeOffset? LastFailedAt { get; init; }

    /// <summary>The policy-selected delay for the next retry, if any.</summary>
    public TimeSpan? RetryDelay { get; init; }

    /// <summary>The verified generation for a successful concrete CShells shell, if known.</summary>
    public long? VerifiedGeneration { get; init; }

    /// <summary>The outcome of the last completed attempt, if any.</summary>
    public ShellActivationAttemptOutcome? LastOutcome { get; init; }

    /// <summary>The last attempt's start time, if any.</summary>
    public DateTimeOffset? LastAttemptStartedAt { get; init; }

    /// <summary>The last attempt's completion time, if any.</summary>
    public DateTimeOffset? LastAttemptCompletedAt { get; init; }

    /// <summary>The next retry deadline, set when the scheduler starts its delay.</summary>
    public DateTimeOffset? NextAttemptAt { get; init; }

    /// <summary>A safe generic code for the last attempt error.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>The last attempt exception type name without its message.</summary>
    public string? ErrorType { get; init; }

    /// <summary>A safe code describing a retry-policy error, if one occurred.</summary>
    public string? PolicyErrorCode { get; init; }
}
