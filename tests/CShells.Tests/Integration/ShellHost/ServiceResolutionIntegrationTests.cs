using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.ShellHost;

/// <summary>
/// Integration tests for service resolution with real feature startup classes.
/// </summary>
public class ServiceResolutionIntegrationTests : IDisposable
{
    private readonly List<CShells.DefaultShellHost> _hostsToDispose = [];

    public void Dispose()
    {
        foreach (var host in _hostsToDispose)
        {
            host.Dispose();
        }
    }

    [Theory(DisplayName = "GetShell with Weather feature resolves expected services")]
    [InlineData(typeof(TestFixtures.IWeatherService), "Weather feature should register IWeatherService")]
    [InlineData(typeof(TestFixtures.ITimeService), "Core feature (dependency of Weather) should register ITimeService")]
    public void GetShell_WithWeatherFeature_ResolvesExpectedServices(Type serviceType, string reason)
    {
        // Arrange
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var shell = host.GetShell(new("Default"));
        var service = shell.ServiceProvider.GetService(serviceType);

        // Assert
        service.Should().NotBeNull(reason);
    }

    [Fact(DisplayName = "GetShell WeatherService can access TimeService")]
    public void GetShell_WeatherService_CanAccessTimeService()
    {
        // Arrange
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var shell = host.GetShell(new("Default"));
        var weatherService = shell.ServiceProvider.GetRequiredService<TestFixtures.IWeatherService>();

        // Assert: WeatherService should have ITimeService injected
        weatherService.TimeService.Should().NotBeNull();
        weatherService.TimeService.Should().BeOfType<TestFixtures.TimeService>();
    }

    [Fact(DisplayName = "GetShell TimeService returns recent time")]
    public void GetShell_TimeService_ReturnsRecentTime()
    {
        // Arrange
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);
        // Use a larger buffer to avoid flakiness in CI environments
        var beforeTime = DateTime.UtcNow.AddSeconds(-5);

        // Act
        var shell = host.GetShell(new("Default"));
        var weatherService = shell.ServiceProvider.GetRequiredService<TestFixtures.IWeatherService>();
        var currentTime = weatherService.TimeService.GetCurrentTime();
        var afterTime = DateTime.UtcNow.AddSeconds(5);

        // Assert: The time should be recent (within reasonable bounds)
        currentTime.Should().BeAfter(beforeTime);
        currentTime.Should().BeBefore(afterTime);
    }

    [Fact(DisplayName = "GetShell WeatherService generates valid weather report")]
    public void GetShell_WeatherService_GeneratesValidWeatherReport()
    {
        // Arrange
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var shell = host.GetShell(new("Default"));
        var weatherService = shell.ServiceProvider.GetRequiredService<TestFixtures.IWeatherService>();
        var report = weatherService.GetWeatherReport();

        // Assert
        report.Should().NotBeNullOrEmpty();
        report.Should().Contain("Weather report generated at");
        report.Should().Contain("UTC");
    }
}
