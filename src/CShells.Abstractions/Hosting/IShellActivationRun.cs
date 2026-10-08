namespace CShells.Hosting;

/// <summary>Handle for one independently owned shell-activation run.</summary>
/// <remarks>
/// A run never drains registry shells. Call <see cref="StopAsync"/> to stop scheduling and request
/// cancellation. If a registry activation ignores cancellation, a caller-supplied shutdown token can
/// bound the wait while the run continues tracking and observing that operation.
/// </remarks>
public interface IShellActivationRun
{
    /// <summary>Completes after every initial target has received its serial first attempt.</summary>
    /// <remarks>
    /// The task is canceled if run cancellation prevents the initial pass from completing. Retry
    /// timers are armed only after this task completes successfully.
    /// </remarks>
    Task InitialPass { get; }

    /// <summary>Gets an immutable safe snapshot of each target, reconciling eligible external shells first.</summary>
    /// <remarks>
    /// Snapshot access is safe from an attempt observer callback. It never exposes exception objects,
    /// exception messages, or stacks. Failed, policy-stopped, and policy-error targets may still be
    /// recognized as externally satisfied until this run is explicitly stopped; satisfaction is terminal.
    /// </remarks>
    IReadOnlyList<ShellActivationAttemptState> Snapshot { get; }

    /// <summary>Requests run cancellation and waits for owned work until <paramref name="shutdownToken"/> cancels.</summary>
    /// <param name="shutdownToken">Bounds this caller's wait; it does not cancel the run's cancellation request.</param>
    /// <returns>A task that completes when tracked run work and its lifetime resources have finished.</returns>
    Task StopAsync(CancellationToken shutdownToken = default);
}
