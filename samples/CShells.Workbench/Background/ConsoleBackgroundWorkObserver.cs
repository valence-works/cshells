namespace CShells.Workbench.Background;

/// <summary>
/// A simple console-based implementation of IBackgroundWorkObserver
/// that logs background work execution to the console.
/// </summary>
public class ConsoleBackgroundWorkObserver(ILogger<ConsoleBackgroundWorkObserver> logger) : IBackgroundWorkObserver
{
    public void OnWorkExecuted(ShellId shellId, string workDescription)
    {
        logger.LogInformation("[Background Work] {WorkDescription}", workDescription);
    }
}
