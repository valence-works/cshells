using FluentAssertions;

namespace CShells.Tests.Integration.ShellHost;

/// <summary>
/// Tests for DefaultShell property behavior with real feature startup classes.
/// </summary>
public class DefaultShellTests : IDisposable
{
    private readonly List<CShells.DefaultShellHost> _hostsToDispose = [];

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
        defaultShell.Should().BeSameAs(getShellResult);
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
        var host = new CShells.DefaultShellHost(shellSettings, [assembly]);
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
        var host = TestFixtures.CreateDefaultHostWithWeatherFeature(_hostsToDispose);

        // Act
        var firstCall = host.DefaultShell;
        var secondCall = host.DefaultShell;
        var thirdCall = host.DefaultShell;

        // Assert
        firstCall.Should().BeSameAs(secondCall);
        secondCall.Should().BeSameAs(thirdCall);
    }
}
