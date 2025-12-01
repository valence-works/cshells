using CShells.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Tests.Unit.Configuration;

/// <summary>
/// Tests for <see cref="ShellSettingsCache"/> and <see cref="ShellSettingsCacheInitializer"/>.
/// </summary>
public class ShellSettingsCacheTests
{
    [Fact(DisplayName = "GetAll returns empty collection when cache is empty")]
    public void GetAll_WhenCacheEmpty_ReturnsEmpty()
    {
        // Arrange
        var cache = new ShellSettingsCache();

        // Act
        var result = cache.GetAll();

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "GetById returns null when cache is empty")]
    public void GetById_WhenCacheEmpty_ReturnsNull()
    {
        // Arrange
        var cache = new ShellSettingsCache();

        // Act
        var result = cache.GetById(new ShellId("Shell1"));

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Initializer loads shells from provider into cache")]
    public async Task Initializer_LoadsShellsFromProvider()
    {
        // Arrange
        var shells = new List<ShellSettings>
        {
            new() { Id = new ShellId("Shell1"), EnabledFeatures = [] },
            new() { Id = new ShellId("Shell2"), EnabledFeatures = [] }
        };
        var provider = new TestShellSettingsProvider(shells);
        var cache = new ShellSettingsCache();
        var initializer = new ShellSettingsCacheInitializer(provider, cache, NullLogger<ShellSettingsCacheInitializer>.Instance);

        // Act
        await initializer.StartAsync(CancellationToken.None);

        // Assert
        var allShells = cache.GetAll();
        Assert.Equal(2, allShells.Count);
        Assert.Contains(allShells, s => s.Id == new ShellId("Shell1"));
        Assert.Contains(allShells, s => s.Id == new ShellId("Shell2"));
    }

    [Fact(DisplayName = "Load populates cache with shells")]
    public void Load_PopulatesCacheWithShells()
    {
        // Arrange
        var shells = new List<ShellSettings>
        {
            new() { Id = new ShellId("Shell1"), EnabledFeatures = [] }
        };
        var cache = new ShellSettingsCache();

        // Act
        cache.Load(shells);

        // Assert
        var result = cache.GetById(new ShellId("Shell1"));
        Assert.NotNull(result);
        Assert.Equal(new ShellId("Shell1"), result.Id);
    }

    [Fact(DisplayName = "GetById returns null when shell not found")]
    public void GetById_WhenShellDoesNotExist_ReturnsNull()
    {
        // Arrange
        var cache = new ShellSettingsCache();
        cache.Load([]);

        // Act
        var result = cache.GetById(new ShellId("NonExistent"));

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Initializer StopAsync clears cache")]
    public async Task Initializer_StopAsync_ClearsCache()
    {
        // Arrange
        var shells = new List<ShellSettings>
        {
            new() { Id = new ShellId("Shell1"), EnabledFeatures = [] }
        };
        var provider = new TestShellSettingsProvider(shells);
        var cache = new ShellSettingsCache();
        var initializer = new ShellSettingsCacheInitializer(provider, cache, NullLogger<ShellSettingsCacheInitializer>.Instance);
        await initializer.StartAsync(CancellationToken.None);

        // Act
        await initializer.StopAsync(CancellationToken.None);

        // Assert
        var result = cache.GetAll();
        Assert.Empty(result);
    }

    [Fact(DisplayName = "Clear removes all shells from cache")]
    public void Clear_RemovesAllShells()
    {
        // Arrange
        var shells = new List<ShellSettings>
        {
            new() { Id = new ShellId("Shell1"), EnabledFeatures = [] }
        };
        var cache = new ShellSettingsCache();
        cache.Load(shells);

        // Act
        cache.Clear();

        // Assert
        var result = cache.GetAll();
        Assert.Empty(result);
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
