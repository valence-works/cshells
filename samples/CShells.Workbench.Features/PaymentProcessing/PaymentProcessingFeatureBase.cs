using CShells.AspNetCore.Features;
using CShells.Workbench.Features.Core;
using CShells.Workbench.Features.FraudDetection;
using CShells.Workbench.Features.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CShells.Workbench.Features.PaymentProcessing;

/// <summary>
/// Base class for payment processing features that exposes the /payments endpoint.
/// </summary>
public abstract class PaymentProcessingFeatureBase : IWebShellFeature
{
    public abstract void ConfigureServices(IServiceCollection services);

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        // Expose /payments endpoint
        endpoints.MapPost("/payments", async (HttpContext context) =>
        {
            // Parse request body
            var request = await context.Request.ReadFromJsonAsync<PaymentRequest>();
            if (request == null)
            {
                return Results.BadRequest(new { Error = "Invalid request body" });
            }

            var tenantInfo = context.RequestServices.GetRequiredService<ITenantInfo>();
            var paymentProcessor = context.RequestServices.GetRequiredService<IPaymentProcessor>();
            var notificationService = context.RequestServices.GetRequiredService<INotificationService>();
            var logger = context.RequestServices.GetRequiredService<IAuditLogger>();

            logger.LogInfo($"Processing payment for {tenantInfo.TenantName}");

            // Check for fraud detection if available (premium feature)
            var fraudDetection = context.RequestServices.GetService<IFraudDetectionService>();
            FraudAnalysisResult? fraudAnalysis = null;

            if (fraudDetection != null)
            {
                fraudAnalysis = fraudDetection.AnalyzeTransaction(
                    request.Amount,
                    request.Currency,
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                );

                if (fraudAnalysis.IsSuspicious)
                {
                    logger.LogInfo($"Suspicious transaction detected: {fraudAnalysis.RiskScore}");
                }
            }

            // Process the payment
            var paymentResult = paymentProcessor.ProcessPayment(request.Amount, request.Currency);

            // Send notification
            await notificationService.SendAsync(
                request.CustomerEmail,
                $"Payment of {request.Amount} {request.Currency} processed successfully via {paymentProcessor.ProcessorName}"
            );

            return Results.Json(new
            {
                Tenant = tenantInfo.TenantName,
                Payment = paymentResult,
                FraudAnalysis = fraudAnalysis,
                NotificationChannel = notificationService.Channel
            });
        });
    }
}

/// <summary>
/// Payment request DTO.
/// </summary>
public record PaymentRequest(decimal Amount, string Currency, string CustomerEmail);
