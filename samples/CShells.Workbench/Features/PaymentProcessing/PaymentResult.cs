namespace CShells.Workbench.Features.PaymentProcessing;

/// <summary>
/// Represents the result of a payment processing operation.
/// </summary>
public class PaymentResult
{
    public required bool Success { get; init; }
    public required string TransactionId { get; init; }
    public required string ProcessorName { get; init; }
    public string? Message { get; init; }
}
