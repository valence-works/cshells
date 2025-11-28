using CShells;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.SampleApp.Features;

/// <summary>
/// Weather service interface for getting weather forecasts.
/// </summary>
public interface IWeatherService
{
    /// <summary>
    /// Gets a weather forecast.
    /// </summary>
    IEnumerable<WeatherForecast> GetForecast();
}

/// <summary>
/// Weather forecast record.
/// </summary>
public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC * 9.0 / 5.0);
}

/// <summary>
/// Implementation of the weather service.
/// </summary>
public class WeatherService : IWeatherService
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    /// <inheritdoc />
    public IEnumerable<WeatherForecast> GetForecast()
    {
        return Enumerable.Range(1, 5).Select(index =>
            new WeatherForecast(
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                Summaries[Random.Shared.Next(Summaries.Length)]
            ));
    }
}

/// <summary>
/// Weather feature startup that registers weather-related services.
/// </summary>
[ShellFeature("Weather", DependsOn = ["Core"], DisplayName = "Weather Feature")]
public class WeatherFeatureStartup : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IWeatherService, WeatherService>();
    }
}
