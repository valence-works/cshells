using CShells.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for <see cref="ServiceCollectionExtensions"/>.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact(DisplayName = "AddCShellsAspNetCore registers default IShellResolver")]
    public void AddCShellsAspNetCore_RegistersDefaultResolver()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCShellsAspNetCore();
        var serviceProvider = services.BuildServiceProvider();
        var resolver = serviceProvider.GetService<IShellResolver>();

        // Assert
        Assert.NotNull(resolver);
    }

    [Fact(DisplayName = "AddCShellsAspNetCore default resolver returns Default ShellId")]
    public void AddCShellsAspNetCore_DefaultResolver_ReturnsDefaultShellId()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCShellsAspNetCore();
        var serviceProvider = services.BuildServiceProvider();
        var resolver = serviceProvider.GetRequiredService<IShellResolver>();
        var httpContext = new DefaultHttpContext();

        // Act
        var result = resolver.Resolve(httpContext);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new("Default"), result.Value);
    }

    [Fact(DisplayName = "AddCShellsAspNetCore does not override custom IShellResolver")]
    public void AddCShellsAspNetCore_WithCustomResolver_DoesNotOverride()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IShellResolver, CustomShellResolver>();

        // Act
        services.AddCShellsAspNetCore();
        var serviceProvider = services.BuildServiceProvider();
        var resolver = serviceProvider.GetRequiredService<IShellResolver>();

        // Assert
        Assert.IsType<CustomShellResolver>(resolver);
    }

    [Fact(DisplayName = "AddCShellsAspNetCore with null services throws ArgumentNullException")]
    public void AddCShellsAspNetCore_WithNullServices_ThrowsArgumentNullException()
    {
        // Act & Assert
        IServiceCollection services = null!;
        var ex = Assert.Throws<ArgumentNullException>(() => services.AddCShellsAspNetCore());
        Assert.Equal("services", ex.ParamName);
    }

    [Fact(DisplayName = "AddCShellsAspNetCore returns services for chaining")]
    public void AddCShellsAspNetCore_ReturnsServicesForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddCShellsAspNetCore();

        // Assert
        Assert.Same(services, result);
    }

    private class CustomShellResolver : IShellResolver
    {
        public ShellId? Resolve(HttpContext httpContext) => new ShellId("Custom");
    }
}
