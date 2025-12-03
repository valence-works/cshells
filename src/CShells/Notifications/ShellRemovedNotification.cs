namespace CShells.Notifications;

/// <summary>
/// Notification published when a shell is removed from the system.
/// </summary>
/// <param name="ShellId">The ID of the shell that was removed.</param>
public record ShellRemovedNotification(ShellId ShellId) : INotification;
