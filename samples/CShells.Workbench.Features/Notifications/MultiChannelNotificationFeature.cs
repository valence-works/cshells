using CShells.Features;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Workbench.Features.Notifications;

/// <summary>
/// Multi-channel notification feature that provides both Email and SMS.
/// Demonstrates how a feature can register multiple implementations of the same interface.
/// Exposes /notifications endpoint via base class.
/// </summary>
[ShellFeature("MultiChannelNotification", DependsOn = ["Core"], DisplayName = "Multi-Channel Notifications")]
public class MultiChannelNotificationFeature : NotificationFeatureBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register both email and SMS notification services
        services.AddSingleton<INotificationService, EmailNotificationService>();
        services.AddSingleton<INotificationService, SmsNotificationService>();
    }
}
