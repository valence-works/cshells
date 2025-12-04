using CShells.Features;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Workbench.Features.PaymentProcessing;

/// <summary>
/// Payment processing feature using Stripe.
/// Exposes /payments endpoint via base class.
/// </summary>
[ShellFeature("StripePayment", DependsOn = ["Core"], DisplayName = "Stripe Payment Processing")]
public class StripePaymentFeature : PaymentProcessingFeatureBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPaymentProcessor, StripePaymentProcessor>();
    }
}
