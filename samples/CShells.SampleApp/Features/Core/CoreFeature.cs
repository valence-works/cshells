namespace CShells.SampleApp.Features.Core;

/// <summary>
/// Core feature that registers fundamental services.
/// </summary>
[ShellFeature("Core", DisplayName = "Core Services")]
public class CoreFeature : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ITimeService, TimeService>();
    }
}