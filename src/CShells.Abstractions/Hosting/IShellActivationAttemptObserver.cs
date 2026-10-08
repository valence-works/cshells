namespace CShells.Hosting;

/// <summary>Observes a completed registry activation attempt before any retry is scheduled.</summary>
/// <remarks>
/// The callback runs outside runner synchronization and may read the run's snapshot. It must not
/// await or synchronously block on <see cref="IShellActivationRun.StopAsync"/> for that same run,
/// because Stop joins the callback's tracked work. Retry attempts for different targets may overlap,
/// so observer instances can receive concurrent callbacks. Observer failures are isolated from attempt state.
/// </remarks>
public interface IShellActivationAttemptObserver
{
    /// <summary>Receives the attempt and its transient error details, if any.</summary>
    /// <param name="attempt">Structured result of the completed attempt.</param>
    /// <param name="cancellationToken">The run lifetime token.</param>
    /// <returns>A task that completes when observation is finished.</returns>
    ValueTask OnAttemptCompletedAsync(ShellActivationAttempt attempt, CancellationToken cancellationToken);
}
