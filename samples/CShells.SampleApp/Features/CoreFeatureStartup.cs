namespace CShells.SampleApp.Features;

/// <summary>
/// Time service interface for getting current time.
/// </summary>
public interface ITimeService
{
    /// <summary>
    /// Gets the current time.
    /// </summary>
    DateTime GetCurrentTime();
}

/// <summary>
/// Implementation of the time service.
/// </summary>
public class TimeService : ITimeService
{
    /// <inheritdoc />
    public DateTime GetCurrentTime() => DateTime.UtcNow;
}

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
