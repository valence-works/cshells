using CShells.AspNetCore.Resolvers;
using Microsoft.AspNetCore.Http;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for <see cref="PathShellResolver"/>.
/// </summary>
public class PathShellResolverTests
{
    [Fact(DisplayName = "Resolve with matching path segment returns correct ShellId")]
    public void Resolve_WithMatchingPathSegment_ReturnsCorrectShellId()
    {
        // Arrange
        var pathMap = new Dictionary<string, ShellId>
        {
            ["tenant1"] = new("Tenant1Shell"),
            ["tenant2"] = new("Tenant2Shell")
        };
        var resolver = new PathShellResolver(pathMap);
        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Path = "/tenant1/some/path"
            }
        };

        // Act
        var result = resolver.Resolve(httpContext);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new("Tenant1Shell"), result.Value);
    }

    [Fact(DisplayName = "Resolve with non-matching path segment returns null")]
    public void Resolve_WithNonMatchingPathSegment_ReturnsNull()
    {
        // Arrange
        var pathMap = new Dictionary<string, ShellId>
        {
            ["tenant1"] = new("Tenant1Shell")
        };
        var resolver = new PathShellResolver(pathMap);
        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Path = "/unknown/path"
            }
        };

        // Act
        var result = resolver.Resolve(httpContext);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Resolve with empty path returns null")]
    public void Resolve_WithEmptyPath_ReturnsNull()
    {
        // Arrange
        var pathMap = new Dictionary<string, ShellId>
        {
            ["tenant1"] = new("Tenant1Shell")
        };
        var resolver = new PathShellResolver(pathMap);
        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Path = ""
            }
        };

        // Act
        var result = resolver.Resolve(httpContext);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Resolve with root path returns null")]
    public void Resolve_WithRootPath_ReturnsNull()
    {
        // Arrange
        var pathMap = new Dictionary<string, ShellId>
        {
            ["tenant1"] = new("Tenant1Shell")
        };
        var resolver = new PathShellResolver(pathMap);
        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Path = "/"
            }
        };

        // Act
        var result = resolver.Resolve(httpContext);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Constructor with null pathMap throws ArgumentNullException")]
    public void Constructor_WithNullPathMap_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new PathShellResolver(null!));
        Assert.Equal("pathMap", ex.ParamName);
    }

    [Fact(DisplayName = "Resolve with null httpContext throws ArgumentNullException")]
    public void Resolve_WithNullHttpContext_ThrowsArgumentNullException()
    {
        // Arrange
        var pathMap = new Dictionary<string, ShellId>();
        var resolver = new PathShellResolver(pathMap);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
        Assert.Equal("httpContext", ex.ParamName);
    }

    [Fact(DisplayName = "Resolve with different case path segment returns correct ShellId")]
    public void Resolve_WithDifferentCasePathSegment_ReturnsCorrectShellId()
    {
        // Arrange
        var pathMap = new Dictionary<string, ShellId>
        {
            ["tenant1"] = new("Tenant1Shell")
        };
        var resolver = new PathShellResolver(pathMap);
        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Path = "/TENANT1/some/path"
            }
        };

        // Act
        var result = resolver.Resolve(httpContext);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new("Tenant1Shell"), result.Value);
    }
}
