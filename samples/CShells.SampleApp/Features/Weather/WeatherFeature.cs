namespace CShells.SampleApp.Features.Weather;

/// <summary>
/// Weather feature that registers standard weather service.
/// </summary>
[ShellFeature("Weather", DependsOn = ["Core"], DisplayName = "Standard Weather Feature")]
public class WeatherFeature : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IWeatherService, StandardWeatherService>();
    }
}