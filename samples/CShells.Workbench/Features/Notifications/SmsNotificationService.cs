using CShells.Workbench.Features.Core;

namespace CShells.Workbench.Features.Notifications;

/// <summary>
/// SMS-based notification service implementation.
/// </summary>
public class SmsNotificationService(IAuditLogger logger) : INotificationService
{
    public string Channel => "SMS";

    public Task<NotificationResult> SendAsync(string recipient, string message)
    {
        logger.LogInfo($"Sending SMS to {recipient}: {message}");

        // Simulate SMS sending
        var messageId = $"sms_{Guid.NewGuid():N}";

        return Task.FromResult(new NotificationResult
        {
            Success = true,
            Channel = Channel,
            MessageId = messageId
        });
    }
}
