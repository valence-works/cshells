using CShells.Workbench.Features.Core;

namespace CShells.Workbench.Features.PaymentProcessing;

/// <summary>
/// Stripe payment processor implementation.
/// </summary>
public class StripePaymentProcessor(IAuditLogger logger) : IPaymentProcessor
{
    public string ProcessorName => "Stripe";

    public PaymentResult ProcessPayment(decimal amount, string currency)
    {
        logger.LogInfo($"Processing ${amount} {currency} payment via Stripe");

        // Simulate payment processing
        var transactionId = $"stripe_{Guid.NewGuid():N}";

        return new()
        {
            Success = true,
            TransactionId = transactionId,
            ProcessorName = ProcessorName,
            Message = "Payment processed successfully via Stripe"
        };
    }
}
