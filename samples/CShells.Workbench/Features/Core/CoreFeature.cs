using CShells.AspNetCore.Features;
using CShells.Features;

namespace CShells.SampleApp.Features.Core;

/// <summary>
/// Core feature that registers fundamental services and exposes tenant information endpoint.
/// </summary>
[ShellFeature("Core", DisplayName = "Core Services")]
public class CoreFeature(ShellSettings shellSettings) : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IAuditLogger, AuditLogger>();
        services.AddSingleton<ITimeService, TimeService>();
        services.AddSingleton<ITenantInfo>(new TenantInfo
        {
            TenantId = shellSettings.Id.ToString(),
            TenantName = shellSettings.Id.ToString(),
            Tier = ""
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        // Expose root endpoint that shows tenant information
        endpoints.MapGet("", async (HttpContext context) =>
        {
            var tenantInfo = context.RequestServices.GetRequiredService<ITenantInfo>();

            return Results.Json(new
            {
                Tenant = tenantInfo.TenantName,
                TenantId = tenantInfo.TenantId,
                Tier = tenantInfo.Tier,
                Message = "Welcome to the Payment Processing Platform"
            });
        });
    }
}
