namespace CShells.Workbench.Features.Reporting;

/// <summary>
/// Represents a transaction report.
/// </summary>
public class TransactionReport
{
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
    public required int TotalTransactions { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public required Dictionary<string, int> TransactionsByProcessor { get; init; }
}
