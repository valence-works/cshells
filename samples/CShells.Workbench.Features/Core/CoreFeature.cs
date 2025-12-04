using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CShells.Workbench.Features.Core;

/// <summary>
/// Core feature that registers fundamental services and exposes tenant information endpoint.
/// </summary>
[ShellFeature("Core", DisplayName = "Core Services")]
public class CoreFeature(ShellSettings shellSettings) : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Bind shell-scoped configuration to CoreOptions
        // The IConfiguration is resolved from the service provider (which will be the shell-scoped one)
        services.AddOptions<CoreOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.Bind(options);
            });

        services.AddSingleton<IAuditLogger, AuditLogger>();
        services.AddSingleton<ITimeService, TimeService>();

        // Create TenantInfo from shell-scoped options
        services.AddSingleton<ITenantInfo>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CoreOptions>>().Value;
            return new TenantInfo
            {
                TenantId = shellSettings.Id.ToString(),
                TenantName = shellSettings.Id.ToString(),
                Tier = options.Tier ?? "Standard"
            };
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        // Expose root endpoint that shows tenant information and configuration
        endpoints.MapGet("", async (HttpContext context) =>
        {
            var tenantInfo = context.RequestServices.GetRequiredService<ITenantInfo>();
            var options = context.RequestServices.GetRequiredService<IOptions<CoreOptions>>().Value;

            return Results.Json(new
            {
                Tenant = tenantInfo.TenantName,
                TenantId = tenantInfo.TenantId,
                Tier = tenantInfo.Tier,
                Message = "Welcome to the Payment Processing Platform",
                Configuration = new
                {
                    options.Theme,
                    options.MaxUploadSizeMB,
                    options.ConnectionStringKey
                }
            });
        });
    }
}
