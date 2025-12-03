namespace CShells.Workbench.Features.Reporting;

/// <summary>
/// Represents a reporting service.
/// </summary>
public interface IReportingService
{
    /// <summary>
    /// Generates a transaction report for the specified date range.
    /// </summary>
    TransactionReport GenerateTransactionReport(DateTime startDate, DateTime endDate);
}
