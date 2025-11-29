using CShells.AspNetCore.Resolvers;
using Microsoft.AspNetCore.Http;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for <see cref="HostShellResolver"/>.
/// </summary>
public class HostShellResolverTests
{
    [Fact(DisplayName = "Resolve with matching host returns correct ShellId")]
    public void Resolve_WithMatchingHost_ReturnsCorrectShellId()
    {
        // Arrange
        var hostMap = new Dictionary<string, ShellId>
        {
            ["tenant1.example.com"] = new("Tenant1Shell"),
            ["tenant2.example.com"] = new("Tenant2Shell")
        };
        var resolver = new HostShellResolver(hostMap);
        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Host = new("tenant1.example.com")
            }
        };

        // Act
        var result = resolver.Resolve(httpContext);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new("Tenant1Shell"), result.Value);
    }

    [Fact(DisplayName = "Resolve with non-matching host returns null")]
    public void Resolve_WithNonMatchingHost_ReturnsNull()
    {
        // Arrange
        var hostMap = new Dictionary<string, ShellId>
        {
            ["tenant1.example.com"] = new("Tenant1Shell")
        };
        var resolver = new HostShellResolver(hostMap);
        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Host = new("unknown.example.com")
            }
        };

        // Act
        var result = resolver.Resolve(httpContext);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Resolve with localhost host returns correct ShellId")]
    public void Resolve_WithLocalhost_ReturnsCorrectShellId()
    {
        // Arrange
        var hostMap = new Dictionary<string, ShellId>
        {
            ["localhost"] = new("DefaultShell")
        };
        var resolver = new HostShellResolver(hostMap);
        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Host = new("localhost", 5000)
            }
        };

        // Act
        var result = resolver.Resolve(httpContext);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new("DefaultShell"), result.Value);
    }

    [Fact(DisplayName = "Constructor with null hostMap throws ArgumentNullException")]
    public void Constructor_WithNullHostMap_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new HostShellResolver(null!));
        Assert.Equal("hostMap", ex.ParamName);
    }

    [Fact(DisplayName = "Resolve with null httpContext throws ArgumentNullException")]
    public void Resolve_WithNullHttpContext_ThrowsArgumentNullException()
    {
        // Arrange
        var hostMap = new Dictionary<string, ShellId>();
        var resolver = new HostShellResolver(hostMap);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
        Assert.Equal("httpContext", ex.ParamName);
    }

    [Fact(DisplayName = "Resolve with different case host returns correct ShellId")]
    public void Resolve_WithDifferentCaseHost_ReturnsCorrectShellId()
    {
        // Arrange
        var hostMap = new Dictionary<string, ShellId>
        {
            ["tenant1.example.com"] = new("Tenant1Shell")
        };
        var resolver = new HostShellResolver(hostMap);
        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Host = new("TENANT1.EXAMPLE.COM")
            }
        };

        // Act
        var result = resolver.Resolve(httpContext);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new("Tenant1Shell"), result.Value);
    }
}
