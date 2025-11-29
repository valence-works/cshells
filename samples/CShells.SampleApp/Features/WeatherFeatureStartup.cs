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
/// Standard weather service implementation with realistic forecasts.
/// </summary>
public class StandardWeatherService : IWeatherService
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
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(index)),
                Random.Shared.Next(-20, 55),
                Summaries[Random.Shared.Next(Summaries.Length)]
            ));
    }
}

/// <summary>
/// Tropical weather service implementation with warm weather forecasts.
/// </summary>
public class TropicalWeatherService : IWeatherService
{
    private static readonly string[] Summaries =
    [
        "Sunny", "Partly Cloudy", "Warm", "Hot", "Humid", "Balmy", "Tropical Storm", "Hurricane Warning"
    ];

    /// <inheritdoc />
    public IEnumerable<WeatherForecast> GetForecast()
    {
        return Enumerable.Range(1, 5).Select(index =>
            new WeatherForecast(
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(20, 38),
                Summaries[Random.Shared.Next(Summaries.Length)]
            ));
    }
}

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
