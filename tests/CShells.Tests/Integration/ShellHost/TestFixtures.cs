using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.ShellHost;

/// <summary>
/// Shared test fixtures including service interfaces, implementations, and feature startup classes
/// used across ShellHost integration tests.
/// </summary>
public static class TestFixtures
{
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
    public class WeatherService(ITimeService timeService) : IWeatherService
    {
        public ITimeService TimeService { get; } = timeService ?? throw new ArgumentNullException(nameof(timeService));

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

    #region Helper Methods

    /// <summary>
    /// Creates a minimal root service provider for testing DefaultShellHost.
    /// </summary>
    public static IServiceProvider CreateRootProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a DefaultShellHost configured with the Weather feature for testing.
    /// </summary>
    public static CShells.DefaultShellHost CreateDefaultHostWithWeatherFeature(List<CShells.DefaultShellHost> hostsToDispose)
    {
        var assembly = typeof(TestFixtures).Assembly;
        var shellSettings = new ShellSettings(new("Default"), ["Weather"]);
        var host = new CShells.DefaultShellHost([shellSettings], [assembly], CreateRootProvider());
        hostsToDispose.Add(host);
        return host;
    }

    #endregion
}
