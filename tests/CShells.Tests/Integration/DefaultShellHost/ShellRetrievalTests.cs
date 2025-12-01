using CShells.Hosting;
using CShells.Tests.Integration.ShellHost;
using CShells.Tests.TestHelpers;

namespace CShells.Tests.Integration.DefaultShellHost;

/// <summary>
/// Tests for <see cref="DefaultShellHost"/> shell retrieval operations (GetShell, DefaultShell, AllShells).
/// </summary>
[Collection(nameof(DefaultShellHostCollection))]
public class ShellRetrievalTests(DefaultShellHostFixture fixture)
{
    [Fact(DisplayName = "DefaultShell with no shells throws InvalidOperationException")]
    public void DefaultShell_WithNoShells_ThrowsInvalidOperationException()
    {
        // Arrange
        var host = fixture.CreateHost(Array.Empty<ShellSettings>(), typeof(TestFixtures).Assembly);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _ = host.DefaultShell);
        Assert.Contains("No shells have been configured", ex.Message);
    }

    [Theory(DisplayName = "DefaultShell resolves expected shell")]
    [InlineData("Default", new[] { "Default", "Other" })]
    [InlineData("First", new[] { "First", "Second" })]
    public void DefaultShell_ReturnsExpectedShell(string expected, string[] names)
    {
        // Arrange
        var settings = names.Select(name => new ShellSettings(new(name))).ToArray();
        var host = fixture.CreateHost(settings, typeof(TestFixtures).Assembly);

        // Act
        var shell = host.DefaultShell;

        // Assert
        Assert.Equal(expected, shell.Id.Name);
    }

    [Fact(DisplayName = "GetShell with valid ID returns shell context")]
    public void GetShell_WithValidId_ReturnsShellContext()
    {
        // Arrange
        var host = fixture.CreateHost([new(new("TestShell"))], typeof(TestFixtures).Assembly);

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
        var host = fixture.CreateHost([new(new("TestShell"))], typeof(TestFixtures).Assembly);

        // Act & Assert
        var ex = Assert.Throws<KeyNotFoundException>(() => host.GetShell(new("NonExistent")));
        Assert.Contains("NonExistent", ex.Message);
    }

    [Fact(DisplayName = "GetShell called multiple times returns same instance")]
    public void GetShell_CalledMultipleTimes_ReturnsSameInstance()
    {
        // Arrange
        var host = fixture.CreateHost([new(new("TestShell"))], typeof(TestFixtures).Assembly);

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
        var host = fixture.CreateHost([
            new(new("Shell1")),
            new(new("Shell2")),
            new(new("Shell3"))
        ], typeof(TestFixtures).Assembly);

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
        var host = fixture.CreateHost([new(new("TestShell"), ["UnknownFeature"])], typeof(TestFixtures).Assembly);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => host.GetShell(new("TestShell")));
        Assert.Contains("UnknownFeature", ex.Message);
        Assert.Contains("not found", ex.Message);
    }
}
