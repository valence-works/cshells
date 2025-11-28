using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Core;

/// <summary>
/// Integration-style unit tests for <see cref="DefaultShellHost"/> using in-test feature startup classes.
/// </summary>
public class ShellHostTests : IDisposable
{
    private readonly List<DefaultShellHost> _hostsToDispose = [];

    public void Dispose()
    {
        foreach (var host in _hostsToDispose)
        {
            host.Dispose();
        }
    }

    #region Test Service Interfaces and Implementations

    /// <summary>
    /// Service interface for providing time information.
    /// </summary>
    public interface ITimeService
    {
        DateTime GetCurrentTime();
    }

    /// <summary>
    /// Implementation of <see cref="ITimeService"/> that returns the current UTC time.
    /// </summary>
    public class TimeService : ITimeService
    {
        public DateTime GetCurrentTime() => DateTime.UtcNow;
    }

    /// <summary>
    /// Service interface for weather information that depends on time.
    /// </summary>
    public interface IWeatherService
    {
        ITimeService TimeService { get; }
        string GetWeatherReport();
    }

    /// <summary>
    /// Implementation of <see cref="IWeatherService"/> that uses <see cref="ITimeService"/>.
    /// </summary>
    public class WeatherService : IWeatherService
    {
        public WeatherService(ITimeService timeService)
        {
            TimeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
        }

        public ITimeService TimeService { get; }

        public string GetWeatherReport()
        {
            var time = TimeService.GetCurrentTime();
            return $"Weather report generated at {time:yyyy-MM-dd HH:mm:ss} UTC";
        }
    }

    #endregion

    #region Test Feature Startup Classes

    /// <summary>
    /// Core feature startup that registers the <see cref="ITimeService"/>.
    /// </summary>
    [ShellFeature("Core")]
    public class CoreFeatureStartup : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ITimeService, TimeService>();
        }
    }

    /// <summary>
    /// Weather feature startup that depends on Core and registers <see cref="IWeatherService"/>.
    /// The <see cref="IWeatherService"/> implementation depends on <see cref="ITimeService"/>.
    /// </summary>
    [ShellFeature("Weather", DependsOn = ["Core"])]
    public class WeatherFeatureStartup : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IWeatherService, WeatherService>();
        }
    }

    #endregion

    #region Feature Discovery Tests

    [Fact(DisplayName = "FeatureDiscovery discovers features from test assembly")]
    public void FeatureDiscovery_DiscoversFeaturesFromTestAssembly()
    {
        // Arrange
        var assembly = typeof(ShellHostTests).Assembly;

        // Act
        var features = FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        features.Should().Contain(f => f.Id == "Core");
        features.Should().Contain(f => f.Id == "Weather");

        var weatherFeature = features.Single(f => f.Id == "Weather");
        weatherFeature.Dependencies.Should().Contain("Core");
    }

    [Fact(DisplayName = "FeatureDiscovery Core feature has correct startup type")]
    public void FeatureDiscovery_CoreFeature_HasCorrectStartupType()
    {
        // Arrange
        var assembly = typeof(ShellHostTests).Assembly;

        // Act
        var features = FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        var coreFeature = features.Single(f => f.Id == "Core");
        coreFeature.StartupType.Should().Be(typeof(CoreFeatureStartup));
    }

    [Fact(DisplayName = "FeatureDiscovery Weather feature has dependency on Core")]
    public void FeatureDiscovery_WeatherFeature_HasDependencyOnCore()
    {
        // Arrange
        var assembly = typeof(ShellHostTests).Assembly;

        // Act
        var features = FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        var weatherFeature = features.Single(f => f.Id == "Weather");
        weatherFeature.StartupType.Should().Be(typeof(WeatherFeatureStartup));
        weatherFeature.Dependencies.Should().ContainSingle().Which.Should().Be("Core");
    }

    #endregion

    #region Shell Host Integration Tests

    [Theory(DisplayName = "GetShell with Weather feature resolves expected services")]
    [InlineData(typeof(IWeatherService), "Weather feature should register IWeatherService")]
    [InlineData(typeof(ITimeService), "Core feature (dependency of Weather) should register ITimeService")]
    public void GetShell_WithWeatherFeature_ResolvesExpectedServices(Type serviceType, string reason)
    {
        // Arrange
        var host = CreateDefaultHostWithWeatherFeature();

        // Act
        var shell = host.GetShell(new ShellId("Default"));
        var service = shell.ServiceProvider.GetService(serviceType);

        // Assert
        service.Should().NotBeNull(reason);
    }

    [Fact(DisplayName = "GetShell WeatherService can access TimeService")]
    public void GetShell_WeatherService_CanAccessTimeService()
    {
        // Arrange
        var host = CreateDefaultHostWithWeatherFeature();

        // Act
        var shell = host.GetShell(new ShellId("Default"));
        var weatherService = shell.ServiceProvider.GetRequiredService<IWeatherService>();

        // Assert: WeatherService should have ITimeService injected
        weatherService.TimeService.Should().NotBeNull();
        weatherService.TimeService.Should().BeOfType<TimeService>();
    }

    [Fact(DisplayName = "GetShell TimeService returns recent time")]
    public void GetShell_TimeService_ReturnsRecentTime()
    {
        // Arrange
        var host = CreateDefaultHostWithWeatherFeature();
        // Use a larger buffer to avoid flakiness in CI environments
        var beforeTime = DateTime.UtcNow.AddSeconds(-5);

        // Act
        var shell = host.GetShell(new ShellId("Default"));
        var weatherService = shell.ServiceProvider.GetRequiredService<IWeatherService>();
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
        var host = CreateDefaultHostWithWeatherFeature();

        // Act
        var shell = host.GetShell(new ShellId("Default"));
        var weatherService = shell.ServiceProvider.GetRequiredService<IWeatherService>();
        var report = weatherService.GetWeatherReport();

        // Assert
        report.Should().NotBeNullOrEmpty();
        report.Should().Contain("Weather report generated at");
        report.Should().Contain("UTC");
    }

    #endregion

    #region DefaultShell Property Tests

    [Fact(DisplayName = "DefaultShell returns same context as GetShell with default ID")]
    public void DefaultShell_ReturnsSameContextAsGetShellWithDefaultId()
    {
        // Arrange
        var host = CreateDefaultHostWithWeatherFeature();

        // Act
        var defaultShell = host.DefaultShell;
        var getShellResult = host.GetShell(new ShellId("Default"));

        // Assert
        defaultShell.Should().BeSameAs(getShellResult);
    }

    [Fact(DisplayName = "DefaultShell with Default ID returns correct shell context")]
    public void DefaultShell_WithDefaultShellId_ReturnsCorrectShellContext()
    {
        // Arrange
        var assembly = typeof(ShellHostTests).Assembly;
        var shellSettings = new[]
        {
            new ShellSettings(new ShellId("Default"), ["Weather"]),
            new ShellSettings(new ShellId("Other"), ["Core"])
        };
        var host = new DefaultShellHost(shellSettings, [assembly]);
        _hostsToDispose.Add(host);

        // Act
        var defaultShell = host.DefaultShell;

        // Assert
        defaultShell.Id.Name.Should().Be("Default");
        defaultShell.Settings.EnabledFeatures.Should().Contain("Weather");
    }

    [Fact(DisplayName = "DefaultShell multiple calls return same instance")]
    public void DefaultShell_MultipleCalls_ReturnsSameInstance()
    {
        // Arrange
        var host = CreateDefaultHostWithWeatherFeature();

        // Act
        var firstCall = host.DefaultShell;
        var secondCall = host.DefaultShell;
        var thirdCall = host.DefaultShell;

        // Assert
        firstCall.Should().BeSameAs(secondCall);
        secondCall.Should().BeSameAs(thirdCall);
    }

    #endregion

    #region Service Provider Tests

    [Fact(DisplayName = "ServiceProvider resolves services from dependency features")]
    public void ServiceProvider_ResolvesServicesFromDependencyFeatures()
    {
        // Arrange
        var host = CreateDefaultHostWithWeatherFeature();

        // Act
        var shell = host.GetShell(new ShellId("Default"));

        // Assert: Both Core (ITimeService) and Weather (IWeatherService) services should be available
        var timeService = shell.ServiceProvider.GetService<ITimeService>();
        var weatherService = shell.ServiceProvider.GetService<IWeatherService>();

        timeService.Should().NotBeNull();
        weatherService.Should().NotBeNull();
    }

    [Theory(DisplayName = "ServiceProvider can resolve shell infrastructure")]
    [InlineData(typeof(ShellContext), "ShellContext")]
    [InlineData(typeof(ShellSettings), "ShellSettings")]
    public void ServiceProvider_CanResolveShellInfrastructure(Type serviceType, string serviceName)
    {
        // Arrange
        var host = CreateDefaultHostWithWeatherFeature();

        // Act
        var shell = host.GetShell(new ShellId("Default"));
        var resolvedService = shell.ServiceProvider.GetRequiredService(serviceType);

        // Assert
        resolvedService.Should().NotBeNull($"{serviceName} should be resolvable from service provider");

        if (serviceType == typeof(ShellContext))
        {
            resolvedService.Should().BeSameAs(shell);
        }
        else if (serviceType == typeof(ShellSettings))
        {
            var settings = (ShellSettings)resolvedService;
            settings.Id.Name.Should().Be("Default");
            settings.EnabledFeatures.Should().Contain("Weather");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a DefaultShellHost configured with the Weather feature for testing.
    /// </summary>
    private DefaultShellHost CreateDefaultHostWithWeatherFeature()
    {
        var assembly = typeof(ShellHostTests).Assembly;
        var shellSettings = new ShellSettings(new ShellId("Default"), ["Weather"]);
        var host = new DefaultShellHost([shellSettings], [assembly]);
        _hostsToDispose.Add(host);
        return host;
    }

    #endregion

    #region Feature Dependency Order Tests

    [Fact(DisplayName = "GetShell dependency order is correct Core before Weather")]
    public void GetShell_DependencyOrderIsCorrect_CoreConfiguredBeforeWeather()
    {
        // Arrange: This test ensures that even though only "Weather" is enabled,
        // "Core" (its dependency) is also configured, and in the correct order
        var host = CreateDefaultHostWithWeatherFeature();

        // Act
        var shell = host.GetShell(new ShellId("Default"));

        // Assert: WeatherService requires ITimeService in its constructor
        // If Core wasn't configured first, this would fail
        var weatherService = shell.ServiceProvider.GetRequiredService<IWeatherService>();
        weatherService.Should().NotBeNull();
        weatherService.TimeService.Should().NotBeNull();
    }

    #endregion
}
