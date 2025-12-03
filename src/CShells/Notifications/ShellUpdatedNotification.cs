namespace CShells.Notifications;

/// <summary>
/// Notification published when a shell's configuration is updated.
/// </summary>
/// <param name="Settings">The updated settings for the shell.</param>
public record ShellUpdatedNotification(ShellSettings Settings) : INotification;
