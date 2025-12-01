using CShells.Configuration;
using CShells.Management;
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
        var cache = CreateCache();

        // Act
        var result = cache.GetAll();

        // Assert
        Assert.Empty(result);
    }

    [Theory(DisplayName = "GetById returns null when shell is missing")]
    [InlineData(false)]
    [InlineData(true)]
    public void GetById_WhenShellMissing_ReturnsNull(bool preloadDifferentShell)
    {
        // Arrange
        var cache = preloadDifferentShell
            ? CreateCache(CreateShell("Existing"))
            : CreateCache();

        // Act
        var result = cache.GetById(new("Target"));

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Load populates cache with shells")]
    public void Load_PopulatesCacheWithShells()
    {
        // Arrange
        var shells = new[]
        {
            CreateShell("Shell1")
        };
        var cache = new ShellSettingsCache();

        // Act
        cache.Load(shells);

        // Assert
        var result = cache.GetById(new("Shell1"));
        Assert.NotNull(result);
        Assert.Equal(new("Shell1"), result.Id);
    }

    [Fact(DisplayName = "Clear removes all shells from cache")]
    public void Clear_RemovesAllShells()
    {
        // Arrange
        var cache = CreateCache(CreateShell("Shell1"));

        // Act
        cache.Clear();

        // Assert
        var result = cache.GetAll();
        Assert.Empty(result);
    }

    private static ShellSettingsCache CreateCache(params ShellSettings[] shells)
    {
        var cache = new ShellSettingsCache();
        if (shells.Length > 0)
        {
            cache.Load(shells);
        }

        return cache;
    }

    private static ShellSettings CreateShell(string id) => new()
    {
        Id = new(id),
        EnabledFeatures = []
    };
}

public class ShellSettingsCacheInitializerTests
{
    [Fact(DisplayName = "Initializer loads shells from provider into cache")]
    public async Task Initializer_LoadsShellsFromProvider()
    {
        // Arrange
        var shellManager = new TestShellManager();
        var initializer = new ShellSettingsCacheInitializer(shellManager, NullLogger<ShellSettingsCacheInitializer>.Instance);

        // Act
        await initializer.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(shellManager.ReloadAllShellsAsyncCalled);
    }

    private class TestShellManager : IShellManager
    {
        public bool ReloadAllShellsAsyncCalled { get; private set; }

        public Task ReloadAllShellsAsync(CancellationToken cancellationToken = default)
        {
            ReloadAllShellsAsyncCalled = true;
            return Task.CompletedTask;
        }

        public Task AddShellAsync(ShellSettings settings, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task RemoveShellAsync(ShellId shellId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task UpdateShellAsync(ShellSettings settings, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
