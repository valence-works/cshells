using CShells.Workbench.Features.Core;

namespace CShells.Workbench.Features.Notifications;

/// <summary>
/// Email-based notification service implementation.
/// </summary>
public class EmailNotificationService(IAuditLogger logger) : INotificationService
{
    public string Channel => "Email";

    public Task<NotificationResult> SendAsync(string recipient, string message)
    {
        logger.LogInfo($"Sending email to {recipient}: {message}");

        // Simulate email sending
        var messageId = $"email_{Guid.NewGuid():N}";

        return Task.FromResult(new NotificationResult
        {
            Success = true,
            Channel = Channel,
            MessageId = messageId
        });
    }
}
