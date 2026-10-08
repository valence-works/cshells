namespace CShells.Hosting;

/// <summary>Outcome of one registry activation call after current-generation verification.</summary>
public enum ShellActivationAttemptOutcome
{
    /// <summary>The returned shell was verified as the settled current generation.</summary>
    Succeeded,

    /// <summary>The registry activation call failed with an exception.</summary>
    ActivationFailed,

    /// <summary>The registry returned a shell that was not the settled current generation.</summary>
    NotCurrent
}

/// <summary>Transient structured input supplied to retry policies and attempt observers.</summary>
/// <remarks>
/// <see cref="Exception"/> is available only during the policy/observer callback input. Run snapshots
/// project safe fields and never retain this exception as public state.
/// </remarks>
public sealed record ShellActivationAttempt
{
    public ShellActivationAttempt(
        string shellName,
        int attemptNumber,
        ShellActivationAttemptOutcome outcome,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        string? errorCode,
        string? errorType,
        Exception? exception,
        long? verifiedGeneration = null)
    {
        ShellName = shellName;
        AttemptNumber = attemptNumber;
        Outcome = outcome;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        ErrorCode = errorCode;
        ErrorType = errorType;
        Exception = exception;
        VerifiedGeneration = verifiedGeneration;
    }

    /// <summary>The target shell name.</summary>
    public string ShellName { get; }

    /// <summary>The one-based attempt number for this target in the run.</summary>
    public int AttemptNumber { get; }

    /// <summary>The verified result of the attempt.</summary>
    public ShellActivationAttemptOutcome Outcome { get; }

    /// <summary>The attempt start time from the run's configured clock.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>The attempt completion time from the run's configured clock.</summary>
    public DateTimeOffset CompletedAt { get; }

    /// <summary>A safe, generic error code, or <see langword="null"/> when there is no error.</summary>
    public string? ErrorCode { get; }

    /// <summary>The exception type name without its message, or <see langword="null"/> when there is no exception.</summary>
    public string? ErrorType { get; }

    /// <summary>
    /// The transient activation exception for policy/observer classification, or <see langword="null"/>
    /// when the result is not-current or successful. Do not store this value in a public run snapshot.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>The generation verified as current for a successful concrete CShells shell.</summary>
    public long? VerifiedGeneration { get; }
}
