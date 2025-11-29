namespace CShells.SampleApp.Features.Weather;

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