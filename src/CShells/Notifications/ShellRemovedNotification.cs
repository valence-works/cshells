namespace CShells.Notifications;

/// <summary>
/// Notification published when a shell is removed from the system.
/// </summary>
public class ShellRemovedNotification : INotification
{
    /// <summary>
    /// Gets the ID of the shell that was removed.
    /// </summary>
    public ShellId ShellId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellRemovedNotification"/> class.
    /// </summary>
    /// <param name="shellId">The ID of the shell that was removed.</param>
    public ShellRemovedNotification(ShellId shellId)
    {
        ShellId = shellId;
    }
}
