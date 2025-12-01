namespace CShells.Notifications;

/// <summary>
/// Notification published when a shell is added to the system.
/// </summary>
public class ShellAddedNotification : INotification
{
    /// <summary>
    /// Gets the settings for the shell that was added.
    /// </summary>
    public ShellSettings Settings { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellAddedNotification"/> class.
    /// </summary>
    /// <param name="settings">The settings for the shell that was added.</param>
    public ShellAddedNotification(ShellSettings settings)
    {
        Settings = settings;
    }
}
