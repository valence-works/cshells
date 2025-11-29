namespace CShells.SampleApp.Features.Weather;

/// <summary>
/// Tropical weather feature that registers tropical weather service.
/// </summary>
[ShellFeature("TropicalWeather", DependsOn = ["Core"], DisplayName = "Tropical Weather Feature")]
public class TropicalWeatherFeature : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IWeatherService, TropicalWeatherService>();
    }
}