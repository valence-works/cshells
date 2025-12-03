namespace CShells.Workbench.Features.FraudDetection;

/// <summary>
/// Represents a fraud detection service.
/// This is a premium feature available only to certain tenants.
/// </summary>
public interface IFraudDetectionService
{
    /// <summary>
    /// Analyzes a transaction for potential fraud.
    /// </summary>
    FraudAnalysisResult AnalyzeTransaction(decimal amount, string currency, string ipAddress);
}
