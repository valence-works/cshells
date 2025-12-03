using CShells.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.ShellHost;

/// <summary>
/// Tests for service provider behavior with real feature startup classes.
/// </summary>
public class ServiceProviderTests : IDisposable
{
    private readonly List<Hosting.DefaultShellHost> _hostsToDispose = [];

    public void Dispose()
    {
        foreach (var host in _hostsToDispose)
        {
            host.Dispose();
        }
    }

    [Fact(DisplayName = "ServiceProvider resolves services from dependency features")]
    public void ServiceProvider_ResolvesServicesFromDependencyFeatures()
    {
        // Arrange
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var shell = host.GetShell(new("Default"));

        // Assert: Both Core (ITimeService) and Weather (IWeatherService) services should be available
        var timeService = shell.ServiceProvider.GetService<TestFixtures.ITimeService>();
        var weatherService = shell.ServiceProvider.GetService<TestFixtures.IWeatherService>();

        Assert.NotNull(timeService);
        Assert.NotNull(weatherService);
    }

    [Theory(DisplayName = "ServiceProvider can resolve shell infrastructure")]
    [InlineData(typeof(ShellContext))]
    [InlineData(typeof(ShellSettings))]
    public void ServiceProvider_CanResolveShellInfrastructure(Type serviceType)
    {
        // Arrange
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var shell = host.GetShell(new("Default"));
        var resolvedService = shell.ServiceProvider.GetRequiredService(serviceType);

        // Assert
        Assert.NotNull(resolvedService);

        if (serviceType == typeof(ShellContext))
        {
            Assert.Same(shell, resolvedService);
        }
        else if (serviceType == typeof(ShellSettings))
        {
            var settings = (ShellSettings)resolvedService;
            Assert.Equal("Default", settings.Id.Name);
            Assert.Contains("Weather", settings.EnabledFeatures);
        }
    }
}
