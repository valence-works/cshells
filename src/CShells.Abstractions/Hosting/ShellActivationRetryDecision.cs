namespace CShells.Hosting;

/// <summary>Action requested by a shell activation retry policy.</summary>
public enum ShellActivationRetryAction
{
    /// <summary>End scheduling for the target.</summary>
    Stop,

    /// <summary>Schedule another attempt after the decision's positive delay.</summary>
    RetryAfter
}

/// <summary>Retry-policy result for one unsuccessful target attempt.</summary>
public readonly record struct ShellActivationRetryDecision
{
    /// <summary>Creates a retry decision.</summary>
    /// <param name="action">The scheduling action.</param>
    /// <param name="delay">The requested delay; valid RetryAfter decisions require a positive value.</param>
    public ShellActivationRetryDecision(ShellActivationRetryAction action, TimeSpan delay)
    {
        Action = action;
        Delay = delay;
    }

    /// <summary>The requested scheduling action.</summary>
    public ShellActivationRetryAction Action { get; }

    /// <summary>The delay requested when <see cref="Action"/> is <see cref="ShellActivationRetryAction.RetryAfter"/>.</summary>
    public TimeSpan Delay { get; }

    /// <summary>A decision to stop scheduling for the target.</summary>
    public static ShellActivationRetryDecision Stop => new(ShellActivationRetryAction.Stop, TimeSpan.Zero);

    /// <summary>Creates a decision to retry after <paramref name="delay"/>.</summary>
    /// <param name="delay">A strictly positive retry delay.</param>
    /// <returns>A retry-after decision, validated by the run when it is applied.</returns>
    public static ShellActivationRetryDecision RetryAfter(TimeSpan delay)
        => new(ShellActivationRetryAction.RetryAfter, delay);
}
