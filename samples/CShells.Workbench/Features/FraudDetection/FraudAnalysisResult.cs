namespace CShells.Workbench.Features.FraudDetection;

/// <summary>
/// Represents the result of a fraud analysis.
/// </summary>
public class FraudAnalysisResult
{
    public required bool IsSuspicious { get; init; }
    public required double RiskScore { get; init; }
    public required string[] Flags { get; init; }
    public string? Recommendation { get; init; }
}
