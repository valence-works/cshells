using CShells.Configuration;
using CShells.Hosting;

namespace CShells.Tests.Integration.ShellHost;

/// <summary>
/// Tests for DefaultShell property behavior with real feature startup classes.
/// </summary>
public class DefaultShellTests : IDisposable
{
    private readonly List<Hosting.DefaultShellHost> _hostsToDispose = [];

    public void Dispose()
    {
        foreach (var host in _hostsToDispose)
        {
            host.Dispose();
        }
    }

    [Fact(DisplayName = "DefaultShell returns same context as GetShell with default ID")]
    public void DefaultShell_ReturnsSameContextAsGetShellWithDefaultId()
    {
        // Arrange
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var defaultShell = host.DefaultShell;
        var getShellResult = host.GetShell(new("Default"));

        // Assert
        Assert.Same(defaultShell, getShellResult);
    }

    [Fact(DisplayName = "DefaultShell with Default ID returns correct shell context")]
    public void DefaultShell_WithDefaultShellId_ReturnsCorrectShellContext()
    {
        // Arrange
        var assembly = typeof(TestFixtures).Assembly;
        var shellSettings = new[]
        {
            new ShellSettings(new("Default"), ["Weather"]),
            new ShellSettings(new("Other"), ["Core"])
        };
        var cache = new ShellSettingsCache();
        cache.Load(shellSettings);
        var (services, provider) = TestFixtures.CreateRootServices();
        var accessor = TestFixtures.CreateRootServicesAccessor(services);
        var factory = new CShells.Features.DefaultShellFeatureFactory(provider);
        var exclusionRegistry = new ShellServiceExclusionRegistry([]);
        var host = new Hosting.DefaultShellHost(cache, [assembly], provider, accessor, factory, exclusionRegistry);
        _hostsToDispose.Add(host);

        // Act
        var defaultShell = host.DefaultShell;

        // Assert
        Assert.Equal("Default", defaultShell.Id.Name);
        Assert.Contains("Weather", defaultShell.Settings.EnabledFeatures);
    }

    [Fact(DisplayName = "DefaultShell multiple calls return same instance")]
    public void DefaultShell_MultipleCalls_ReturnsSameInstance()
    {
        // Arrange
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var firstCall = host.DefaultShell;
        var secondCall = host.DefaultShell;
        var thirdCall = host.DefaultShell;

        // Assert
        Assert.Same(firstCall, secondCall);
        Assert.Same(secondCall, thirdCall);
    }
}
