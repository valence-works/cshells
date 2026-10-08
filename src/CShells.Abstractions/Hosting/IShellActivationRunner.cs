namespace CShells.Hosting;

/// <summary>Starts opt-in, host-owned activation runs over an explicit set of shell names.</summary>
public interface IShellActivationRunner
{
    /// <summary>Starts a run and returns its handle without running a blocking activation on the caller's stack.</summary>
    /// <remarks>
    /// A successful custom-registry return is treated as that registry's stable success contract and must
    /// still be current at verification. External reconciliation is available only for a current concrete
    /// CShells <c>Shell</c> whose activation is committed.
    /// Initial attempts are serial. Retry work for different targets may overlap, so policy and observer
    /// instances can receive concurrent calls.
    /// </remarks>
    /// <param name="shellNames">Target shell names; validated, copied, and deduplicated case-insensitively in first-occurrence order.</param>
    /// <param name="retryPolicy">Optional policy for unsuccessful attempts; <see langword="null"/> means one attempt per target.</param>
    /// <param name="observer">Optional observer called after each attempt result is recorded.</param>
    /// <param name="startupCancellationToken">Cancels this run's lifetime and prevents future retry scheduling.</param>
    /// <returns>A handle to the independent run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="shellNames"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A target name is null, empty, or whitespace.</exception>
    IShellActivationRun Start(
        IReadOnlyList<string> shellNames,
        ShellActivationRetryPolicy? retryPolicy = null,
        IShellActivationAttemptObserver? observer = null,
        CancellationToken startupCancellationToken = default);
}

/// <summary>Chooses whether an unsuccessful target should stop or retry after a delay.</summary>
/// <remarks>
/// This synchronous callback runs as owned work for its activation run. It must not synchronously
/// block on that same run's <see cref="IShellActivationRun.StopAsync"/>, which joins the callback.
/// Policy calls for different target retry loops may overlap.
/// </remarks>
/// <param name="attempt">The structured attempt, including its transient exception when activation failed.</param>
/// <returns>A stop decision or a retry-after decision.</returns>
public delegate ShellActivationRetryDecision ShellActivationRetryPolicy(ShellActivationAttempt attempt);
