namespace CShells.Workbench.Features.Notifications;

/// <summary>
/// Represents the result of a notification operation.
/// </summary>
public class NotificationResult
{
    public required bool Success { get; init; }
    public required string Channel { get; init; }
    public string? MessageId { get; init; }
    public string? Error { get; init; }
}
