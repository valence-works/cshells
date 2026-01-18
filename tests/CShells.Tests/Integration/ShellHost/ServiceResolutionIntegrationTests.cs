using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.ShellHost;

/// <summary>
/// Integration tests for service resolution with real feature startup classes.
/// </summary>
public class ServiceResolutionIntegrationTests : IDisposable
{
    private readonly List<Hosting.DefaultShellHost> _hostsToDispose = [];

    public void Dispose()
    {
        foreach (var host in _hostsToDispose)
        {
            host.Dispose();
        }
    }

    [Theory(DisplayName = "GetShell with Weather feature resolves expected services")]
    [InlineData(typeof(TestFixtures.IWeatherService))]
    [InlineData(typeof(TestFixtures.ITimeService))]
    public void GetShell_WithWeatherFeature_ResolvesExpectedServices(Type serviceType)
    {
        // Arrange
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var shell = host.GetShell(new("Default"));
        var service = shell.ServiceProvider.GetService(serviceType);

        // Assert
        Assert.NotNull(service);
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
        Assert.NotNull(weatherService.TimeService);
        Assert.IsType<TestFixtures.TimeService>(weatherService.TimeService);
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
        Assert.True(currentTime > beforeTime);
        Assert.True(currentTime < afterTime);
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
        Assert.False(string.IsNullOrEmpty(report));
        Assert.Contains("Weather report generated at", report);
        Assert.Contains("UTC", report);
    }

    [Fact(DisplayName = "Shell can resolve feature descriptors as IReadOnlyCollection")]
    public void Shell_CanResolveFeatureDescriptors_AsIReadOnlyCollection()
    {
        // Arrange
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var shell = host.GetShell(new("Default"));
        var descriptors = shell.ServiceProvider.GetService<IReadOnlyCollection<CShells.Features.ShellFeatureDescriptor>>();

        // Assert
        Assert.NotNull(descriptors);
        Assert.NotEmpty(descriptors);
    }

    [Fact(DisplayName = "Shell can resolve feature descriptors as IEnumerable")]
    public void Shell_CanResolveFeatureDescriptors_AsIEnumerable()
    {
        // Arrange
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var shell = host.GetShell(new("Default"));
        var descriptors = shell.ServiceProvider.GetService<IEnumerable<CShells.Features.ShellFeatureDescriptor>>();

        // Assert
        Assert.NotNull(descriptors);
        Assert.NotEmpty(descriptors);
    }

    [Fact(DisplayName = "Both IReadOnlyCollection and IEnumerable resolve to same instance")]
    public void Shell_FeatureDescriptors_BothInterfacesResolveSameInstance()
    {
        // Arrange
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var shell = host.GetShell(new("Default"));
        var asCollection = shell.ServiceProvider.GetRequiredService<IReadOnlyCollection<CShells.Features.ShellFeatureDescriptor>>();
        var asEnumerable = shell.ServiceProvider.GetRequiredService<IEnumerable<CShells.Features.ShellFeatureDescriptor>>();

        // Assert - both should resolve to the same instance (singleton)
        Assert.Same(asCollection, asEnumerable);
    }
}
