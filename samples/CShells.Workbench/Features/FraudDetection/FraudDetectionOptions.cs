namespace CShells.Workbench.Features.FraudDetection;

/// <summary>
/// Configuration options for the Fraud Detection feature.
/// These are bound from shell-specific configuration.
/// </summary>
public class FraudDetectionOptions
{
    /// <summary>
    /// The risk score threshold above which a transaction is considered suspicious.
    /// Default is 0.7 if not specified in configuration.
    /// </summary>
    public double Threshold { get; set; } = 0.7;

    /// <summary>
    /// Maximum transaction amount before triggering high-value flag.
    /// Default is 10000 if not specified in configuration.
    /// </summary>
    public decimal MaxTransactionAmount { get; set; } = 10000;
}
