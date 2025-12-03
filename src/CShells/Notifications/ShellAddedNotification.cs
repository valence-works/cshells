namespace CShells.Notifications;

/// <summary>
/// Notification published when a shell is added to the system.
/// </summary>
/// <param name="Settings">The settings for the shell that was added.</param>
public record ShellAddedNotification(ShellSettings Settings) : INotification;
