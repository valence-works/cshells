using CShells.AspNetCore.Configuration;
using CShells.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Tests.Unit.Configuration;

/// <summary>
/// Tests for <see cref="DefaultShellSettingsCache"/>.
/// </summary>
public class DefaultShellSettingsCacheTests
{
    [Fact(DisplayName = "GetAll before initialization throws InvalidOperationException")]
    public void GetAll_BeforeInitialization_ThrowsInvalidOperationException()
    {
        // Arrange
        var provider = new TestShellSettingsProvider([]);
        var cache = new DefaultShellSettingsCache(provider, NullLogger<DefaultShellSettingsCache>.Instance);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => cache.GetAll());
        Assert.Contains("not been initialized", ex.Message);
    }

    [Fact(DisplayName = "GetById before initialization throws InvalidOperationException")]
    public void GetById_BeforeInitialization_ThrowsInvalidOperationException()
    {
        // Arrange
        var provider = new TestShellSettingsProvider([]);
        var cache = new DefaultShellSettingsCache(provider, NullLogger<DefaultShellSettingsCache>.Instance);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => cache.GetById(new ShellId("test")));
        Assert.Contains("not been initialized", ex.Message);
    }

    [Fact(DisplayName = "StartAsync loads shells from provider")]
    public async Task StartAsync_LoadsShellsFromProvider()
    {
        // Arrange
        var shells = new List<ShellSettings>
        {
            new() { Id = new ShellId("Shell1"), EnabledFeatures = [] },
            new() { Id = new ShellId("Shell2"), EnabledFeatures = [] }
        };
        var provider = new TestShellSettingsProvider(shells);
        var cache = new DefaultShellSettingsCache(provider, NullLogger<DefaultShellSettingsCache>.Instance);

        // Act
        await cache.StartAsync(CancellationToken.None);

        // Assert
        var allShells = cache.GetAll();
        Assert.Equal(2, allShells.Count);
        Assert.Contains(allShells, s => s.Id == new ShellId("Shell1"));
        Assert.Contains(allShells, s => s.Id == new ShellId("Shell2"));
    }

    [Fact(DisplayName = "GetById returns shell when found")]
    public async Task GetById_WhenShellExists_ReturnsShell()
    {
        // Arrange
        var shells = new List<ShellSettings>
        {
            new() { Id = new ShellId("Shell1"), EnabledFeatures = [] }
        };
        var provider = new TestShellSettingsProvider(shells);
        var cache = new DefaultShellSettingsCache(provider, NullLogger<DefaultShellSettingsCache>.Instance);
        await cache.StartAsync(CancellationToken.None);

        // Act
        var result = cache.GetById(new ShellId("Shell1"));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new ShellId("Shell1"), result.Id);
    }

    [Fact(DisplayName = "GetById returns null when shell not found")]
    public async Task GetById_WhenShellDoesNotExist_ReturnsNull()
    {
        // Arrange
        var provider = new TestShellSettingsProvider([]);
        var cache = new DefaultShellSettingsCache(provider, NullLogger<DefaultShellSettingsCache>.Instance);
        await cache.StartAsync(CancellationToken.None);

        // Act
        var result = cache.GetById(new ShellId("NonExistent"));

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "StopAsync clears cache")]
    public async Task StopAsync_ClearsCache()
    {
        // Arrange
        var shells = new List<ShellSettings>
        {
            new() { Id = new ShellId("Shell1"), EnabledFeatures = [] }
        };
        var provider = new TestShellSettingsProvider(shells);
        var cache = new DefaultShellSettingsCache(provider, NullLogger<DefaultShellSettingsCache>.Instance);
        await cache.StartAsync(CancellationToken.None);

        // Act
        await cache.StopAsync(CancellationToken.None);

        // Assert
        var ex = Assert.Throws<InvalidOperationException>(() => cache.GetAll());
        Assert.Contains("not been initialized", ex.Message);
    }

    [Fact(DisplayName = "Constructor with null provider throws ArgumentNullException")]
    public void Constructor_WithNullProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new DefaultShellSettingsCache(null!));
        Assert.Equal("provider", ex.ParamName);
    }

    private class TestShellSettingsProvider : IShellSettingsProvider
    {
        private readonly List<ShellSettings> _shells;

        public TestShellSettingsProvider(List<ShellSettings> shells)
        {
            _shells = shells;
        }

        public Task<IEnumerable<ShellSettings>> GetShellSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<ShellSettings>>(_shells);
        }
    }
}
