namespace CShells.Workbench.Features.Notifications;

/// <summary>
/// Represents a notification service.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Gets the notification channel name.
    /// </summary>
    string Channel { get; }

    /// <summary>
    /// Sends a notification with the specified message.
    /// </summary>
    Task<NotificationResult> SendAsync(string recipient, string message);
}
