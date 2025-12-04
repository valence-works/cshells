using CShells.Workbench.Features.Core;
using Microsoft.Extensions.Options;

namespace CShells.Workbench.Features.FraudDetection;

/// <summary>
/// Fraud detection service implementation.
/// Uses shell-scoped configuration options to customize fraud detection behavior.
/// </summary>
public class FraudDetectionService(IAuditLogger logger, IOptions<FraudDetectionOptions> options) : IFraudDetectionService
{
    private readonly FraudDetectionOptions _options = options.Value;

    public FraudAnalysisResult AnalyzeTransaction(decimal amount, string currency, string ipAddress)
    {
        logger.LogInfo($"Analyzing transaction: ${amount} {currency} from {ipAddress} (Threshold: {_options.Threshold})");

        // Simulate fraud detection logic using configured thresholds
        var flags = new List<string>();
        var riskScore = 0.0;

        // Use configured max transaction amount
        if (amount > _options.MaxTransactionAmount)
        {
            flags.Add($"High transaction amount (>${_options.MaxTransactionAmount})");
            riskScore += 0.3;
        }

        if (ipAddress.StartsWith("192.168"))
        {
            flags.Add("Local IP address");
            riskScore += 0.1;
        }
        else
        {
            flags.Add("External IP address");
            riskScore += 0.2;
        }

        // Use configured threshold to determine if suspicious
        var isSuspicious = riskScore > _options.Threshold;

        return new()
        {
            IsSuspicious = isSuspicious,
            RiskScore = Math.Round(riskScore, 2),
            Flags = flags.ToArray(),
            Recommendation = isSuspicious
                ? "Manual review recommended"
                : "Transaction appears safe to process"
        };
    }
}
