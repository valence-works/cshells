namespace CShells.SampleApp.Background;

/// <summary>
/// Observer interface for monitoring background work execution.
/// Used by background workers to notify observers of work being performed.
/// </summary>
public interface IBackgroundWorkObserver
{
    /// <summary>
    /// Called when background work is executed for a shell.
    /// </summary>
    /// <param name="shellId">The ID of the shell for which work was executed.</param>
    /// <param name="workDescription">A description of the work that was executed.</param>
    void OnWorkExecuted(ShellId shellId, string workDescription);
}
