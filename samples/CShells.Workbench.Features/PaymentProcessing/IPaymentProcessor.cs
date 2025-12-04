namespace CShells.Workbench.Features.PaymentProcessing;

/// <summary>
/// Represents a payment processing service.
/// </summary>
public interface IPaymentProcessor
{
    /// <summary>
    /// Gets the name of the payment processor.
    /// </summary>
    string ProcessorName { get; }

    /// <summary>
    /// Processes a payment for the specified amount.
    /// </summary>
    PaymentResult ProcessPayment(decimal amount, string currency);
}
