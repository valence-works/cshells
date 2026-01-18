using CShells.Configuration;
using CShells.DependencyInjection;
using CShells.Features;
using CShells.Hosting;
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
    /// Creates a minimal root service collection and provider for testing DefaultShellHost.
    /// </summary>
    public static (IServiceCollection Services, IServiceProvider Provider) CreateRootServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        return (services, provider);
    }

    /// <summary>
    /// Creates an <see cref="IRootServiceCollectionAccessor"/> for testing.
    /// </summary>
    public static IRootServiceCollectionAccessor CreateRootServicesAccessor(IServiceCollection services)
    {
        return new TestRootServiceCollectionAccessor(services);
    }

    /// <summary>
    /// Creates a DefaultShellHost configured with the Weather feature for testing.
    /// </summary>
    public static Hosting.DefaultShellHost CreateDefaultHostWithWeatherFeature(List<Hosting.DefaultShellHost> hostsToDispose)
    {
        var assembly = typeof(TestFixtures).Assembly;
        var shellSettings = new ShellSettings(new("Default"), ["Weather"]);
        var cache = new ShellSettingsCache();
        cache.Load([shellSettings]);
        var (services, provider) = CreateRootServices();
        var accessor = CreateRootServicesAccessor(services);
        var factory = new CShells.Features.DefaultShellFeatureFactory(provider);
        var exclusionRegistry = new ShellServiceExclusionRegistry([]);
        var host = new Hosting.DefaultShellHost(cache, [assembly], provider, accessor, factory, exclusionRegistry);
        hostsToDispose.Add(host);
        return host;
    }

    /// <summary>
    /// Test implementation of <see cref="IRootServiceCollectionAccessor"/>.
    /// </summary>
    private sealed class TestRootServiceCollectionAccessor(IServiceCollection services) : IRootServiceCollectionAccessor
    {
        public IServiceCollection Services { get; } = services;
    }

    #endregion
}
