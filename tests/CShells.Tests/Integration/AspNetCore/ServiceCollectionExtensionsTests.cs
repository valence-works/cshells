using CShells.DependencyInjection;
using CShells.Resolution;
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
        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services);
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
        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services);
        var serviceProvider = services.BuildServiceProvider();
        var resolver = serviceProvider.GetRequiredService<IShellResolver>();
        var context = new ShellResolutionContext();

        // Act
        var result = resolver.Resolve(context);

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
        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services);
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
        var ex = Assert.Throws<ArgumentNullException>(() => CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services));
        Assert.Equal("services", ex.ParamName);
    }

    [Fact(DisplayName = "AddCShellsAspNetCore returns builder for chaining")]
    public void AddCShellsAspNetCore_ReturnsBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<CShellsBuilder>(result);
        Assert.Same(services, result.Services);
    }

    [Fact(DisplayName = "AddCShellsAspNetCore registers multiple strategies including standard resolvers")]
    public void AddCShellsAspNetCore_RegistersMultipleStrategies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IShellResolverStrategy, CustomStrategy>();

        // Act
        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services);
        var serviceProvider = services.BuildServiceProvider();
        var strategies = serviceProvider.GetServices<IShellResolverStrategy>().ToList();

        // Assert - should have custom strategy, unified web routing resolver, and default fallback strategy
        Assert.Equal(3, strategies.Count);
        Assert.Contains(strategies, s => s is CustomStrategy);
        Assert.Contains(strategies, s => s.GetType().Name == "WebRoutingShellResolver");
        Assert.Contains(strategies, s => s is DefaultShellResolverStrategy);
    }

    [Fact(DisplayName = "DefaultShellResolver orchestrates multiple strategies in order")]
    public void DefaultShellResolver_OrchestatesStrategiesInOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IShellResolverStrategy, NullStrategy>(); // Returns null
        services.AddSingleton<IShellResolverStrategy, CustomStrategy>(); // Returns "Custom"
        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services);

        var serviceProvider = services.BuildServiceProvider();
        var resolver = serviceProvider.GetRequiredService<IShellResolver>();
        var context = new ShellResolutionContext();

        // Act
        var result = resolver.Resolve(context);

        // Assert - should resolve "Custom" since NullStrategy returns null first
        Assert.NotNull(result);
        Assert.Equal(new("Custom"), result.Value);
    }

    private class CustomShellResolver : IShellResolver
    {
        public ShellId? Resolve(ShellResolutionContext context) => new ShellId("Custom");
    }

    private class CustomStrategy : IShellResolverStrategy
    {
        public ShellId? Resolve(ShellResolutionContext context) => new ShellId("Custom");
    }

    private class NullStrategy : IShellResolverStrategy
    {
        public ShellId? Resolve(ShellResolutionContext context) => null;
    }
}
