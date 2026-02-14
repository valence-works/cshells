using CShells.AspNetCore;
using CShells.AspNetCore.Resolution;
using CShells.Configuration;
using CShells.Resolution;
using System.Text.Json;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Integration tests for the unified WebRoutingShellResolver.
/// </summary>
public class WebRoutingShellResolverTests
{
    private const string Tenant1Name = "Tenant1";
    private const string Tenant2Name = "Tenant2";
    private const string Tenant3Name = "Tenant3";
    private const string Tenant1Path = "tenant1";
    private const string Tenant1Host = "tenant1.example.com";
    private const string HeaderName = "X-Tenant-Id";
    private const string ClaimKey = "tenant_id";

    #region Path Routing Tests

    [Fact(DisplayName = "WebRoutingShellResolver resolves shell by path")]
    public void WebRoutingShellResolver_WithPath_ReturnsShellId()
    {
        // Arrange
        var cache = CreateCacheWithPathShell();
        var options = new WebRoutingShellResolverOptions();
        var resolver = new WebRoutingShellResolver(cache, options);
        var context = CreateContext(path: $"/{Tenant1Path}/api");

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Equal(new ShellId(Tenant1Name), result);
    }

    [Fact(DisplayName = "WebRoutingShellResolver path matching is case-insensitive")]
    public void WebRoutingShellResolver_PathMatching_IsCaseInsensitive()
    {
        // Arrange
        var cache = CreateCacheWithPathShell();
        var options = new WebRoutingShellResolverOptions();
        var resolver = new WebRoutingShellResolver(cache, options);
        var context = CreateContext(path: $"/{Tenant1Path.ToUpper()}/api");

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Equal(new ShellId(Tenant1Name), result);
    }

    [Fact(DisplayName = "WebRoutingShellResolver respects excluded paths")]
    public void WebRoutingShellResolver_WithExcludedPath_ReturnsNull()
    {
        // Arrange
        var cache = CreateCacheWithPathShell();
        var options = new WebRoutingShellResolverOptions
        {
            ExcludePaths = ["/api", "/health"]
        };
        var resolver = new WebRoutingShellResolver(cache, options);
        var context = CreateContext(path: "/api/users");

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "WebRoutingShellResolver with path routing disabled returns null")]
    public void WebRoutingShellResolver_PathRoutingDisabled_ReturnsNull()
    {
        // Arrange
        var cache = CreateCacheWithPathShell();
        var options = new WebRoutingShellResolverOptions { EnablePathRouting = false };
        var resolver = new WebRoutingShellResolver(cache, options);
        var context = CreateContext(path: $"/{Tenant1Path}/api");

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Host Routing Tests

    [Fact(DisplayName = "WebRoutingShellResolver resolves shell by host")]
    public void WebRoutingShellResolver_WithHost_ReturnsShellId()
    {
        // Arrange
        var cache = CreateCacheWithHostShell();
        var options = new WebRoutingShellResolverOptions();
        var resolver = new WebRoutingShellResolver(cache, options);
        var context = CreateContext(host: Tenant1Host);

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Equal(new ShellId(Tenant1Name), result);
    }

    [Fact(DisplayName = "WebRoutingShellResolver host matching is case-insensitive")]
    public void WebRoutingShellResolver_HostMatching_IsCaseInsensitive()
    {
        // Arrange
        var cache = CreateCacheWithHostShell();
        var options = new WebRoutingShellResolverOptions();
        var resolver = new WebRoutingShellResolver(cache, options);
        var context = CreateContext(host: Tenant1Host.ToUpper());

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Equal(new ShellId(Tenant1Name), result);
    }

    [Fact(DisplayName = "WebRoutingShellResolver with host routing disabled returns null")]
    public void WebRoutingShellResolver_HostRoutingDisabled_ReturnsNull()
    {
        // Arrange
        var cache = CreateCacheWithHostShell();
        var options = new WebRoutingShellResolverOptions { EnableHostRouting = false };
        var resolver = new WebRoutingShellResolver(cache, options);
        var context = CreateContext(host: Tenant1Host);

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Header Routing Tests

    [Fact(DisplayName = "WebRoutingShellResolver resolves shell by header")]
    public void WebRoutingShellResolver_WithHeader_ReturnsShellId()
    {
        // Arrange
        var cache = CreateCacheWithHeaderShell();
        var options = new WebRoutingShellResolverOptions { HeaderName = HeaderName };
        var resolver = new WebRoutingShellResolver(cache, options);
        var context = CreateContext();
        context.Set($"Header:{HeaderName}", Tenant2Name);

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Equal(new ShellId(Tenant2Name), result);
    }

    [Fact(DisplayName = "WebRoutingShellResolver header matching is case-insensitive")]
    public void WebRoutingShellResolver_HeaderMatching_IsCaseInsensitive()
    {
        // Arrange
        var cache = CreateCacheWithHeaderShell();
        var options = new WebRoutingShellResolverOptions { HeaderName = HeaderName };
        var resolver = new WebRoutingShellResolver(cache, options);
        var context = CreateContext();
        context.Set($"Header:{HeaderName}", Tenant2Name.ToUpper());

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Equal(new ShellId(Tenant2Name), result);
    }

    [Fact(DisplayName = "WebRoutingShellResolver without header name returns null")]
    public void WebRoutingShellResolver_WithoutHeaderName_ReturnsNull()
    {
        // Arrange
        var cache = CreateCacheWithHeaderShell();
        var options = new WebRoutingShellResolverOptions(); // No HeaderName set
        var resolver = new WebRoutingShellResolver(cache, options);
        var context = CreateContext();
        context.Set($"Header:{HeaderName}", Tenant2Name);

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Claim Routing Tests

    [Fact(DisplayName = "WebRoutingShellResolver resolves shell by claim")]
    public void WebRoutingShellResolver_WithClaim_ReturnsShellId()
    {
        // Arrange
        var cache = CreateCacheWithClaimShell();
        var options = new WebRoutingShellResolverOptions { ClaimKey = ClaimKey };
        var resolver = new WebRoutingShellResolver(cache, options);
        var context = CreateContext();
        context.Set($"Claim:{ClaimKey}", Tenant3Name);

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Equal(new ShellId(Tenant3Name), result);
    }

    [Fact(DisplayName = "WebRoutingShellResolver claim matching is case-insensitive")]
    public void WebRoutingShellResolver_ClaimMatching_IsCaseInsensitive()
    {
        // Arrange
        var cache = CreateCacheWithClaimShell();
        var options = new WebRoutingShellResolverOptions { ClaimKey = ClaimKey };
        var resolver = new WebRoutingShellResolver(cache, options);
        var context = CreateContext();
        context.Set($"Claim:{ClaimKey}", Tenant3Name.ToUpper());

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Equal(new ShellId(Tenant3Name), result);
    }

    [Fact(DisplayName = "WebRoutingShellResolver without claim key returns null")]
    public void WebRoutingShellResolver_WithoutClaimKey_ReturnsNull()
    {
        // Arrange
        var cache = CreateCacheWithClaimShell();
        var options = new WebRoutingShellResolverOptions(); // No ClaimKey set
        var resolver = new WebRoutingShellResolver(cache, options);
        var context = CreateContext();
        context.Set($"Claim:{ClaimKey}", Tenant3Name);

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Multi-Method Tests

    [Fact(DisplayName = "WebRoutingShellResolver tries methods in order: path, host, header, claim")]
    public void WebRoutingShellResolver_TriesMethodsInOrder()
    {
        // Arrange - shell with path configuration
        var cache = CreateCacheWithPathShell();
        var options = new WebRoutingShellResolverOptions
        {
            HeaderName = HeaderName,
            ClaimKey = ClaimKey
        };
        var resolver = new WebRoutingShellResolver(cache, options);

        // Context with path (should match first)
        var context = CreateContext(path: $"/{Tenant1Path}/api");
        context.Set($"Header:{HeaderName}", "WrongTenant");
        context.Set($"Claim:{ClaimKey}", "WrongTenant");

        // Act
        var result = resolver.Resolve(context);

        // Assert - Should resolve by path (first method)
        Assert.Equal(new ShellId(Tenant1Name), result);
    }

    [Fact(DisplayName = "WebRoutingShellResolver falls through to next method if first fails")]
    public void WebRoutingShellResolver_FallsThroughToNextMethod()
    {
        // Arrange - shell with header configuration
        var cache = CreateCacheWithHeaderShell();
        var options = new WebRoutingShellResolverOptions
        {
            HeaderName = HeaderName
        };
        var resolver = new WebRoutingShellResolver(cache, options);

        // Context with no path/host match, but header match
        var context = CreateContext(path: "/other/api", host: "other.example.com");
        context.Set($"Header:{HeaderName}", Tenant2Name);

        // Act
        var result = resolver.Resolve(context);

        // Assert - Should resolve by header (fallback method)
        Assert.Equal(new ShellId(Tenant2Name), result);
    }

    #endregion

    #region Validation Tests

    [Fact(DisplayName = "WebRoutingShellResolver constructor with null cache throws ArgumentNullException")]
    public void WebRoutingShellResolver_Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new WebRoutingShellResolverOptions();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new WebRoutingShellResolver(null!, options));
        Assert.Equal("cache", ex.ParamName);
    }

    [Fact(DisplayName = "WebRoutingShellResolver constructor with null options throws ArgumentNullException")]
    public void WebRoutingShellResolver_Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var cache = CreateCacheWithPathShell();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new WebRoutingShellResolver(cache, null!));
        Assert.Equal("options", ex.ParamName);
    }

    [Fact(DisplayName = "WebRoutingShellResolver with null context throws ArgumentNullException")]
    public void WebRoutingShellResolver_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var cache = CreateCacheWithPathShell();
        var options = new WebRoutingShellResolverOptions();
        var resolver = new WebRoutingShellResolver(cache, options);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
    }

    #endregion

    #region Helper Methods

    private static ShellResolutionContext CreateContext(string? path = null, string? host = null)
    {
        var context = new ShellResolutionContext();
        if (path != null)
            context.Set(ShellResolutionContextKeys.Path, path);
        if (host != null)
            context.Set(ShellResolutionContextKeys.Host, host);
        return context;
    }

    private static IShellSettingsCache CreateCacheWithPathShell()
    {
        var routingOptions = new WebRoutingShellOptions { Path = Tenant1Path };
        return new TestShellSettingsCache([CreateShell(Tenant1Name, routingOptions)]);
    }

    private static IShellSettingsCache CreateCacheWithHostShell()
    {
        var routingOptions = new WebRoutingShellOptions { Host = Tenant1Host };
        return new TestShellSettingsCache([CreateShell(Tenant1Name, routingOptions)]);
    }

    private static IShellSettingsCache CreateCacheWithHeaderShell()
    {
        var routingOptions = new WebRoutingShellOptions { HeaderName = HeaderName };
        return new TestShellSettingsCache([CreateShell(Tenant2Name, routingOptions)]);
    }

    private static IShellSettingsCache CreateCacheWithClaimShell()
    {
        var routingOptions = new WebRoutingShellOptions { ClaimKey = ClaimKey };
        return new TestShellSettingsCache([CreateShell(Tenant3Name, routingOptions)]);
    }

    private static ShellSettings CreateShell(string name, WebRoutingShellOptions routingOptions)
    {
        var settings = new ShellSettings
        {
            Id = new(name),
            EnabledFeatures = [],
            ConfigurationData = new Dictionary<string, object>()
        };

        // Flatten WebRouting options into ConfigurationData
        if (routingOptions.Path != null)
            settings.ConfigurationData["WebRouting:Path"] = routingOptions.Path;
        if (routingOptions.Host != null)
            settings.ConfigurationData["WebRouting:Host"] = routingOptions.Host;
        if (routingOptions.HeaderName != null)
            settings.ConfigurationData["WebRouting:HeaderName"] = routingOptions.HeaderName;
        if (routingOptions.ClaimKey != null)
            settings.ConfigurationData["WebRouting:ClaimKey"] = routingOptions.ClaimKey;

        return settings;
    }

    private class TestShellSettingsCache(List<ShellSettings> shells) : IShellSettingsCache
    {
        public IReadOnlyCollection<ShellSettings> GetAll() => shells;
        public ShellSettings? GetById(ShellId id) => shells.FirstOrDefault(s => s.Id == id);
    }

    #endregion
}
