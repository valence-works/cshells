using CShells.AspNetCore.Features;
using CShells.Features;
using CShells.Workbench.Features.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CShells.Workbench.Features.FraudDetection;

/// <summary>
/// Fraud detection feature - a premium feature available only to premium/enterprise tenants.
/// Exposes /fraud-check endpoint.
/// </summary>
[ShellFeature("FraudDetection", DependsOn = ["Core"], DisplayName = "Fraud Detection")]
public class FraudDetectionFeature : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Bind shell-scoped configuration to FraudDetectionOptions
        // Settings are now directly under the feature name (e.g., "FraudDetection:Threshold")
        services.AddOptions<FraudDetectionOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection("FraudDetection").Bind(options);
            });

        services.AddSingleton<IFraudDetectionService, FraudDetectionService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        // Expose /fraud-check endpoint
        endpoints.MapPost("/fraud-check", async (HttpContext context) =>
        {
            // Parse request body
            var request = await context.Request.ReadFromJsonAsync<FraudCheckRequest>();
            if (request == null)
            {
                return Results.BadRequest(new { Error = "Invalid request body" });
            }

            var tenantInfo = context.RequestServices.GetRequiredService<ITenantInfo>();
            var fraudDetection = context.RequestServices.GetRequiredService<IFraudDetectionService>();
            var options = context.RequestServices.GetRequiredService<IOptions<FraudDetectionOptions>>().Value;

            var result = fraudDetection.AnalyzeTransaction(request.Amount, request.Currency, request.IpAddress);

            return Results.Json(new
            {
                Tenant = tenantInfo.TenantName,
                Analysis = result,
                Configuration = new
                {
                    options.Threshold,
                    options.MaxTransactionAmount
                }
            });
        });
    }
}

/// <summary>
/// Fraud check request DTO.
/// </summary>
public record FraudCheckRequest(decimal Amount, string Currency, string IpAddress);
