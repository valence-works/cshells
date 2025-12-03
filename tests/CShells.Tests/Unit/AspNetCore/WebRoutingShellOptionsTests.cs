using CShells.AspNetCore;

namespace CShells.Tests.Unit.AspNetCore;

public class WebRoutingShellOptionsTests
{
    [Fact(DisplayName = "Default constructor initializes with null properties")]
    public void DefaultConstructor_InitializesWithNullProperties()
    {
        // Act
        var options = new WebRoutingShellOptions();

        // Assert
        Assert.Null(options.Path);
        Assert.Null(options.Host);
        Assert.Null(options.HeaderName);
        Assert.Null(options.ClaimKey);
    }

    [Fact(DisplayName = "Path property can be set and retrieved")]
    public void PathProperty_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new WebRoutingShellOptions();
        const string path = "tenant1";

        // Act
        options.Path = path;

        // Assert
        Assert.Equal(path, options.Path);
    }

    [Fact(DisplayName = "Host property can be set and retrieved")]
    public void HostProperty_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new WebRoutingShellOptions();
        const string host = "tenant1.example.com";

        // Act
        options.Host = host;

        // Assert
        Assert.Equal(host, options.Host);
    }

    [Fact(DisplayName = "HeaderName property can be set and retrieved")]
    public void HeaderNameProperty_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new WebRoutingShellOptions();
        const string headerName = "X-Tenant-Id";

        // Act
        options.HeaderName = headerName;

        // Assert
        Assert.Equal(headerName, options.HeaderName);
    }

    [Fact(DisplayName = "ClaimKey property can be set and retrieved")]
    public void ClaimKeyProperty_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new WebRoutingShellOptions();
        const string claimKey = "tenant_id";

        // Act
        options.ClaimKey = claimKey;

        // Assert
        Assert.Equal(claimKey, options.ClaimKey);
    }

    [Fact(DisplayName = "All properties can be set via object initializer")]
    public void AllProperties_CanBeSetViaObjectInitializer()
    {
        // Act
        var options = new WebRoutingShellOptions
        {
            Path = "tenant1",
            Host = "tenant1.example.com",
            HeaderName = "X-Tenant-Id",
            ClaimKey = "tenant_id"
        };

        // Assert
        Assert.Equal("tenant1", options.Path);
        Assert.Equal("tenant1.example.com", options.Host);
        Assert.Equal("X-Tenant-Id", options.HeaderName);
        Assert.Equal("tenant_id", options.ClaimKey);
    }

    [Fact(DisplayName = "Properties can be updated after initialization")]
    public void Properties_CanBeUpdatedAfterInitialization()
    {
        // Arrange
        var options = new WebRoutingShellOptions
        {
            Path = "initial"
        };

        // Act
        options.Path = "updated";
        options.Host = "example.com";

        // Assert
        Assert.Equal("updated", options.Path);
        Assert.Equal("example.com", options.Host);
    }

    [Fact(DisplayName = "Properties can be set to null")]
    public void Properties_CanBeSetToNull()
    {
        // Arrange
        var options = new WebRoutingShellOptions
        {
            Path = "tenant1",
            Host = "example.com",
            HeaderName = "X-Header",
            ClaimKey = "claim"
        };

        // Act
        options.Path = null;
        options.Host = null;
        options.HeaderName = null;
        options.ClaimKey = null;

        // Assert
        Assert.Null(options.Path);
        Assert.Null(options.Host);
        Assert.Null(options.HeaderName);
        Assert.Null(options.ClaimKey);
    }
}
