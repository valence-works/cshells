using CShells.AspNetCore.Features;
using CShells.Workbench.Features.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CShells.Workbench.Features.Notifications;

/// <summary>
/// Base class for notification features that exposes the /notifications endpoint.
/// </summary>
public abstract class NotificationFeatureBase : IWebShellFeature
{
    public abstract void ConfigureServices(IServiceCollection services);

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        // Expose /notifications endpoint
        endpoints.MapPost("/notifications", async (HttpContext context) =>
        {
            // Parse request body
            var request = await context.Request.ReadFromJsonAsync<NotificationRequest>();
            if (request == null)
            {
                return Results.BadRequest(new { Error = "Invalid request body" });
            }

            var tenantInfo = context.RequestServices.GetRequiredService<ITenantInfo>();

            // For tenants with multi-channel notifications, get all available services
            var notificationServices = context.RequestServices.GetServices<INotificationService>().ToList();

            if (!notificationServices.Any())
            {
                return Results.Problem(
                    detail: "No notification service available",
                    statusCode: 500
                );
            }

            var results = new List<object>();

            foreach (var service in notificationServices)
            {
                var result = await service.SendAsync(request.Recipient, request.Message);
                results.Add(new
                {
                    Channel = service.Channel,
                    Result = result
                });
            }

            return Results.Json(new
            {
                Tenant = tenantInfo.TenantName,
                ChannelsUsed = notificationServices.Select(s => s.Channel).ToArray(),
                Results = results
            });
        });
    }
}

/// <summary>
/// Notification request DTO.
/// </summary>
public record NotificationRequest(string Recipient, string Message);
