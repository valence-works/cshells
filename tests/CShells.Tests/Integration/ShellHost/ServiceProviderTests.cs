using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.ShellHost;

/// <summary>
/// Tests for service provider behavior with real feature startup classes.
/// </summary>
public class ServiceProviderTests : IDisposable
{
    private readonly List<CShells.DefaultShellHost> _hostsToDispose = [];

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

        timeService.Should().NotBeNull();
        weatherService.Should().NotBeNull();
    }

    [Theory(DisplayName = "ServiceProvider can resolve shell infrastructure")]
    [InlineData(typeof(ShellContext), "ShellContext")]
    [InlineData(typeof(ShellSettings), "ShellSettings")]
    public void ServiceProvider_CanResolveShellInfrastructure(Type serviceType, string serviceName)
    {
        // Arrange
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var shell = host.GetShell(new("Default"));
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
}
