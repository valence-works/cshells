using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CShells.Workbench.Features.Reporting;

/// <summary>
/// Reporting feature that demonstrates IWebShellFeature for exposing endpoints.
/// This feature exposes its own /reports endpoint within the shell's service scope.
/// </summary>
[ShellFeature("Reporting", DependsOn = ["Core"], DisplayName = "Transaction Reporting")]
public class ReportingFeature : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IReportingService, ReportingService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        // Expose a /reports endpoint that uses the shell-scoped reporting service
        endpoints.MapGet("/reports", async (HttpContext context) =>
        {
            var reportingService = context.RequestServices.GetRequiredService<IReportingService>();

            // Get optional date range from query string, default to last 30 days
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-30);

            if (context.Request.Query.TryGetValue("startDate", out var startDateStr) &&
                DateTime.TryParse(startDateStr, out var parsedStartDate))
            {
                startDate = parsedStartDate;
            }

            if (context.Request.Query.TryGetValue("endDate", out var endDateStr) &&
                DateTime.TryParse(endDateStr, out var parsedEndDate))
            {
                endDate = parsedEndDate;
            }

            var report = reportingService.GenerateTransactionReport(startDate, endDate);

            return Results.Json(report);
        });
    }
}
