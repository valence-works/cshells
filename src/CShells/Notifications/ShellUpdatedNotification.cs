namespace CShells.Notifications;

/// <summary>
/// Notification published when a shell's configuration is updated.
/// </summary>
public class ShellUpdatedNotification : INotification
{
    /// <summary>
    /// Gets the updated settings for the shell.
    /// </summary>
    public ShellSettings Settings { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellUpdatedNotification"/> class.
    /// </summary>
    /// <param name="settings">The updated settings for the shell.</param>
    public ShellUpdatedNotification(ShellSettings settings)
    {
        Settings = settings;
    }
}
