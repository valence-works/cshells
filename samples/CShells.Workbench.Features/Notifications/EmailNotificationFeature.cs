using CShells.Features;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Workbench.Features.Notifications;

/// <summary>
/// Email notification feature.
/// Exposes /notifications endpoint via base class.
/// </summary>
[ShellFeature("EmailNotification", DependsOn = ["Core"], DisplayName = "Email Notifications")]
public class EmailNotificationFeature : NotificationFeatureBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<INotificationService, EmailNotificationService>();
    }
}
