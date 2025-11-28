using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.ShellHost;

/// <summary>
/// Tests for verifying correct feature dependency ordering with real feature startup classes.
/// </summary>
public class DependencyOrderTests : IDisposable
{
    private readonly List<CShells.DefaultShellHost> _hostsToDispose = [];

    public void Dispose()
    {
        foreach (var host in _hostsToDispose)
        {
            host.Dispose();
        }
    }

    [Fact(DisplayName = "GetShell dependency order is correct Core before Weather")]
    public void GetShell_DependencyOrderIsCorrect_CoreConfiguredBeforeWeather()
    {
        // Arrange: This test ensures that even though only "Weather" is enabled,
        // "Core" (its dependency) is also configured, and in the correct order
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var shell = host.GetShell(new("Default"));

        // Assert: WeatherService requires ITimeService in its constructor
        // If Core wasn't configured first, this would fail
        var weatherService = shell.ServiceProvider.GetRequiredService<TestFixtures.IWeatherService>();
        weatherService.Should().NotBeNull();
        weatherService.TimeService.Should().NotBeNull();
    }
}
