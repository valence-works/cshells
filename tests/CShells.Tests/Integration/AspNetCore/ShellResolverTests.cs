using CShells.AspNetCore;
using CShells.AspNetCore.Resolvers;
using Microsoft.AspNetCore.Http;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Consolidated tests for all <see cref="IShellResolver"/> implementations.
/// </summary>
public class ShellResolverTests
{
    private const string Tenant1Host = "tenant1.example.com";
    private const string Tenant2Host = "tenant2.example.com";
    private const string Tenant1Path = "tenant1";
    private const string Tenant2Path = "tenant2";
    private const string Localhost = "localhost";

    public static TheoryData<IShellResolver, HttpContext, ShellId?, string> ResolverTestCases => new()
    {
        // HostShellResolver - matching hosts
        { CreateHostResolver(), CreateContext(Tenant1Host), new ShellId("Tenant1Shell"), "HostResolver with matching host" },
        { CreateHostResolver(), CreateContext(Tenant2Host), new ShellId("Tenant2Shell"), "HostResolver with second matching host" },
        { CreateHostResolver(), CreateContext(Localhost, 5000), new ShellId("LocalhostShell"), "HostResolver with localhost and port" },
        { CreateHostResolver(), CreateContext("TENANT1.EXAMPLE.COM"), new ShellId("Tenant1Shell"), "HostResolver case-insensitive host" },

        // HostShellResolver - non-matching
        { CreateHostResolver(), CreateContext("unknown.example.com"), null, "HostResolver with non-matching host" },

        // PathShellResolver - matching paths
        { CreatePathResolver(), CreateContext(path: $"/{Tenant1Path}/some/path"), new ShellId("Tenant1Shell"), "PathResolver with matching first segment" },
        { CreatePathResolver(), CreateContext(path: $"/{Tenant2Path}/api"), new ShellId("Tenant2Shell"), "PathResolver with matching second segment" },
        { CreatePathResolver(), CreateContext(path: $"/{Tenant1Path}"), new ShellId("Tenant1Shell"), "PathResolver with single segment" },
        { CreatePathResolver(), CreateContext(path: "/TENANT1/path"), new ShellId("Tenant1Shell"), "PathResolver case-insensitive path" },

        // PathShellResolver - non-matching
        { CreatePathResolver(), CreateContext(path: "/unknown/path"), null, "PathResolver with non-matching path" },
        { CreatePathResolver(), CreateContext(path: ""), null, "PathResolver with empty path" },
        { CreatePathResolver(), CreateContext(path: "/"), null, "PathResolver with root path" },
    };

    [Theory(DisplayName = "Resolve with various inputs returns expected result")]
    [MemberData(nameof(ResolverTestCases))]
    public void Resolve_WithVariousInputs_ReturnsExpectedResult(
        IShellResolver resolver,
        HttpContext context,
        ShellId? expectedShellId,
        string scenario)
    {
        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.Equal(expectedShellId, result);
    }

    [Fact(DisplayName = "HostShellResolver constructor with null hostMap throws ArgumentNullException")]
    public void HostShellResolver_Constructor_WithNullHostMap_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new HostShellResolver(null!));
        Assert.Equal("hostMap", ex.ParamName);
    }

    [Fact(DisplayName = "PathShellResolver constructor with null pathMap throws ArgumentNullException")]
    public void PathShellResolver_Constructor_WithNullPathMap_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new PathShellResolver(null!));
        Assert.Equal("pathMap", ex.ParamName);
    }

    [Theory(DisplayName = "Resolve with null httpContext throws ArgumentNullException")]
    [InlineData(typeof(HostShellResolver))]
    [InlineData(typeof(PathShellResolver))]
    public void Resolve_WithNullHttpContext_ThrowsArgumentNullException(Type resolverType)
    {
        // Arrange
        var resolver = CreateResolverInstance(resolverType);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
        Assert.Equal("httpContext", ex.ParamName);
    }

    #region Helper Methods

    private static HostShellResolver CreateHostResolver() => new(new Dictionary<string, ShellId>
    {
        [Tenant1Host] = new("Tenant1Shell"),
        [Tenant2Host] = new("Tenant2Shell"),
        [Localhost] = new("LocalhostShell")
    });

    private static PathShellResolver CreatePathResolver() => new(new Dictionary<string, ShellId>
    {
        [Tenant1Path] = new("Tenant1Shell"),
        [Tenant2Path] = new("Tenant2Shell")
    });

    private static IShellResolver CreateResolverInstance(Type resolverType)
    {
        var emptyMap = new Dictionary<string, ShellId>();
        return (IShellResolver)Activator.CreateInstance(resolverType, emptyMap)!;
    }

    private static HttpContext CreateContext(string? host = null, int? port = null, string? path = null)
    {
        var context = new DefaultHttpContext();

        if (host != null)
            context.Request.Host = port.HasValue ? new HostString(host, port.Value) : new HostString(host);

        if (path != null)
            context.Request.Path = path;

        return context;
    }

    #endregion
}
