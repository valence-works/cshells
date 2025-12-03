using CShells.Features;

namespace CShells.Workbench.Features.Notifications;

/// <summary>
/// SMS notification feature.
/// Exposes /notifications endpoint via base class.
/// </summary>
[ShellFeature("SmsNotification", DependsOn = ["Core"], DisplayName = "SMS Notifications")]
public class SmsNotificationFeature : NotificationFeatureBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<INotificationService, SmsNotificationService>();
    }
}
