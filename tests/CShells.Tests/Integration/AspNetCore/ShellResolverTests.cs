using CShells.AspNetCore;
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Resolution;
using CShells.Configuration;
using CShells.Resolution;
using System.Text.Json;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for shell resolver implementations.
/// </summary>
public class ShellResolverTests
{
    private const string Tenant1Host = "tenant1.example.com";
    private const string Tenant2Host = "tenant2.example.com";
    private const string Tenant1Path = "tenant1";
    private const string Tenant2Path = "tenant2";

    [Fact(DisplayName = "HostShellResolver resolves shell by host property")]
    public void HostShellResolver_WithMatchingHost_ReturnsShellId()
    {
        // Arrange
        var cache = CreateCacheWithHostShells();
        var resolver = new HostShellResolver(cache);
        var context = CreateResolutionContext(host: Tenant1Host);

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Equal(new ShellId("Tenant1Shell"), result);
    }

    [Fact(DisplayName = "HostShellResolver with non-matching host returns null")]
    public void HostShellResolver_WithNonMatchingHost_ReturnsNull()
    {
        // Arrange
        var cache = CreateCacheWithHostShells();
        var resolver = new HostShellResolver(cache);
        var context = CreateResolutionContext(host: "unknown.example.com");

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "HostShellResolver is case-insensitive")]
    public void HostShellResolver_WithDifferentCase_ReturnsShellId()
    {
        // Arrange
        var cache = CreateCacheWithHostShells();
        var resolver = new HostShellResolver(cache);
        var context = CreateResolutionContext(host: "TENANT1.EXAMPLE.COM");

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Equal(new ShellId("Tenant1Shell"), result);
    }

    [Fact(DisplayName = "PathShellResolver resolves shell by path property")]
    public void PathShellResolver_WithMatchingPath_ReturnsShellId()
    {
        // Arrange
        var cache = CreateCacheWithPathShells();
        var resolver = new PathShellResolver(cache);
        var context = CreateResolutionContext(path: $"/{Tenant1Path}/api/users");

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Equal(new ShellId("Tenant1Shell"), result);
    }

    [Fact(DisplayName = "PathShellResolver with non-matching path returns null")]
    public void PathShellResolver_WithNonMatchingPath_ReturnsNull()
    {
        // Arrange
        var cache = CreateCacheWithPathShells();
        var resolver = new PathShellResolver(cache);
        var context = CreateResolutionContext(path: "/unknown/api");

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "PathShellResolver is case-insensitive")]
    public void PathShellResolver_WithDifferentCase_ReturnsShellId()
    {
        // Arrange
        var cache = CreateCacheWithPathShells();
        var resolver = new PathShellResolver(cache);
        var context = CreateResolutionContext(path: "/TENANT1/api");

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Equal(new ShellId("Tenant1Shell"), result);
    }

    [Fact(DisplayName = "HostShellResolver constructor with null cache throws ArgumentNullException")]
    public void HostShellResolver_Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new HostShellResolver(null!));
        Assert.Equal("cache", ex.ParamName);
    }

    [Fact(DisplayName = "PathShellResolver constructor with null cache throws ArgumentNullException")]
    public void PathShellResolver_Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new PathShellResolver(null!));
        Assert.Equal("cache", ex.ParamName);
    }

    #region Helper Methods

    private static IShellSettingsCache CreateCacheWithHostShells()
    {
        var shells = new List<ShellSettings>
        {
            new ShellSettings
            {
                Id = new ShellId("Tenant1Shell"),
                EnabledFeatures = [],
                Properties = new Dictionary<string, object>
                {
                    [ShellPropertyKeys.Host] = JsonDocument.Parse($"\"{Tenant1Host}\"").RootElement
                }
            },
            new ShellSettings
            {
                Id = new ShellId("Tenant2Shell"),
                EnabledFeatures = [],
                Properties = new Dictionary<string, object>
                {
                    [ShellPropertyKeys.Host] = JsonDocument.Parse($"\"{Tenant2Host}\"").RootElement
                }
            }
        };

        return new TestShellSettingsCache(shells);
    }

    private static IShellSettingsCache CreateCacheWithPathShells()
    {
        var shells = new List<ShellSettings>
        {
            new ShellSettings
            {
                Id = new ShellId("Tenant1Shell"),
                EnabledFeatures = [],
                Properties = new Dictionary<string, object>
                {
                    [ShellPropertyKeys.Path] = JsonDocument.Parse($"\"{Tenant1Path}\"").RootElement
                }
            },
            new ShellSettings
            {
                Id = new ShellId("Tenant2Shell"),
                EnabledFeatures = [],
                Properties = new Dictionary<string, object>
                {
                    [ShellPropertyKeys.Path] = JsonDocument.Parse($"\"{Tenant2Path}\"").RootElement
                }
            }
        };

        return new TestShellSettingsCache(shells);
    }

    private static ShellResolutionContext CreateResolutionContext(string? host = null, string? path = null)
    {
        var context = new ShellResolutionContext();

        if (host != null)
            context.Set(ShellResolutionContextKeys.Host, host);

        if (path != null)
            context.Set(ShellResolutionContextKeys.Path, path);

        return context;
    }

    private class TestShellSettingsCache : IShellSettingsCache
    {
        private readonly List<ShellSettings> _shells;

        public TestShellSettingsCache(List<ShellSettings> shells)
        {
            _shells = shells;
        }

        public IReadOnlyCollection<ShellSettings> GetAll() => _shells;

        public ShellSettings? GetById(ShellId id) => _shells.FirstOrDefault(s => s.Id == id);
    }

    #endregion
}
