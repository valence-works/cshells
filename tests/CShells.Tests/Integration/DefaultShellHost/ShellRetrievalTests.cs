using CShells.Tests.Integration.ShellHost;

namespace CShells.Tests.Integration.DefaultShellHost;

/// <summary>
/// Tests for <see cref="CShells.DefaultShellHost"/> shell retrieval operations (GetShell, DefaultShell, AllShells).
/// </summary>
public class ShellRetrievalTests : IDisposable
{
    private readonly List<CShells.DefaultShellHost> _hostsToDispose = [];

    public void Dispose()
    {
        foreach (var host in _hostsToDispose)
        {
            host.Dispose();
        }
    }

    private CShells.DefaultShellHost CreateHost(ShellSettings[] settings)
    {
        var (services, provider) = TestFixtures.CreateRootServices();
        var accessor = TestFixtures.CreateRootServicesAccessor(services);
        var host = new CShells.DefaultShellHost(settings, [], provider, accessor);
        _hostsToDispose.Add(host);
        return host;
    }

    [Fact(DisplayName = "DefaultShell with no shells throws InvalidOperationException")]
    public void DefaultShell_WithNoShells_ThrowsInvalidOperationException()
    {
        // Arrange
        var host = CreateHost([]);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _ = host.DefaultShell);
        Assert.Contains("No shells have been configured", ex.Message);
    }

    [Fact(DisplayName = "DefaultShell with Default shell configured returns default shell")]
    public void DefaultShell_WithDefaultShellConfigured_ReturnsDefaultShell()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new("Default")),
            new ShellSettings(new("Other"))
        };
        var host = CreateHost(settings);

        // Act
        var shell = host.DefaultShell;

        // Assert
        Assert.Equal("Default", shell.Id.Name);
    }

    [Fact(DisplayName = "DefaultShell without default shell returns first shell")]
    public void DefaultShell_WithoutDefaultShell_ReturnsFirstShell()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new("First")),
            new ShellSettings(new("Second"))
        };
        var host = CreateHost(settings);

        // Act
        var shell = host.DefaultShell;

        // Assert
        Assert.Equal("First", shell.Id.Name);
    }

    [Fact(DisplayName = "GetShell with valid ID returns shell context")]
    public void GetShell_WithValidId_ReturnsShellContext()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new("TestShell"))
        };
        var host = CreateHost(settings);

        // Act
        var shell = host.GetShell(new("TestShell"));

        // Assert
        Assert.NotNull(shell);
        Assert.Equal("TestShell", shell.Id.Name);
        Assert.NotNull(shell.Settings);
        Assert.NotNull(shell.ServiceProvider);
    }

    [Fact(DisplayName = "GetShell with invalid ID throws KeyNotFoundException")]
    public void GetShell_WithInvalidId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new("TestShell"))
        };
        var host = CreateHost(settings);

        // Act & Assert
        var ex = Assert.Throws<KeyNotFoundException>(() => host.GetShell(new("NonExistent")));
        Assert.Contains("NonExistent", ex.Message);
    }

    [Fact(DisplayName = "GetShell called multiple times returns same instance")]
    public void GetShell_CalledMultipleTimes_ReturnsSameInstance()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new("TestShell"))
        };
        var host = CreateHost(settings);

        // Act
        var shell1 = host.GetShell(new("TestShell"));
        var shell2 = host.GetShell(new("TestShell"));

        // Assert
        Assert.Same(shell1, shell2);
    }

    [Fact(DisplayName = "AllShells returns all configured shells")]
    public void AllShells_ReturnsAllConfiguredShells()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new("Shell1")),
            new ShellSettings(new("Shell2")),
            new ShellSettings(new("Shell3"))
        };
        var host = CreateHost(settings);

        // Act
        var allShells = host.AllShells;

        // Assert
        Assert.Equal(3, allShells.Count);
        Assert.Contains(allShells, s => s.Id.Name == "Shell1");
        Assert.Contains(allShells, s => s.Id.Name == "Shell2");
        Assert.Contains(allShells, s => s.Id.Name == "Shell3");
    }

    [Fact(DisplayName = "GetShell with unknown feature throws InvalidOperationException")]
    public void GetShell_WithUnknownFeature_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new("TestShell"), ["UnknownFeature"])
        };
        var host = CreateHost(settings);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => host.GetShell(new("TestShell")));
        Assert.Contains("UnknownFeature", ex.Message);
        Assert.Contains("not found", ex.Message);
    }
}
