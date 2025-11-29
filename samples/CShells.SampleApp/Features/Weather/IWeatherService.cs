namespace CShells.SampleApp.Features.Weather;

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