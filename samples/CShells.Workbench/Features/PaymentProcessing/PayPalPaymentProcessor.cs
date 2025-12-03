using CShells.Workbench.Features.Core;

namespace CShells.Workbench.Features.PaymentProcessing;

/// <summary>
/// PayPal payment processor implementation.
/// </summary>
public class PayPalPaymentProcessor(IAuditLogger logger) : IPaymentProcessor
{
    public string ProcessorName => "PayPal";

    public PaymentResult ProcessPayment(decimal amount, string currency)
    {
        logger.LogInfo($"Processing ${amount} {currency} payment via PayPal");

        // Simulate payment processing with PayPal-specific logic
        var transactionId = $"pp_{Guid.NewGuid():N}";

        return new()
        {
            Success = true,
            TransactionId = transactionId,
            ProcessorName = ProcessorName,
            Message = "Payment processed successfully via PayPal"
        };
    }
}
