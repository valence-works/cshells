namespace CShells.Notifications;

/// <summary>
/// Notification published when all shells are reloaded from the provider.
/// </summary>
public class ShellsReloadedNotification : INotification
{
    /// <summary>
    /// Gets all shell settings after the reload.
    /// </summary>
    public IReadOnlyCollection<ShellSettings> AllShells { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellsReloadedNotification"/> class.
    /// </summary>
    /// <param name="allShells">All shell settings after the reload.</param>
    public ShellsReloadedNotification(IReadOnlyCollection<ShellSettings> allShells)
    {
        AllShells = allShells;
    }
}
