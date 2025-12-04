using CShells.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Core;

/// <summary>
/// Unit tests for <see cref="IShellContextScope"/> and <see cref="DefaultShellContextScopeFactory"/>.
/// </summary>
public class ShellContextScopeTests
{
    [Fact(DisplayName = "CreateScope with null shell context throws ArgumentNullException")]
    public void CreateScope_WithNullShellContext_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new DefaultShellContextScopeFactory();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => factory.CreateScope(null!));
        Assert.Equal("shellContext", ex.ParamName);
    }

    [Fact(DisplayName = "CreateScope returns scope with correct ShellContext")]
    public void CreateScope_WithValidShellContext_ReturnsScopeWithCorrectShellContext()
    {
        // Arrange
        var services = new ServiceCollection();
        using var serviceProvider = services.BuildServiceProvider();
        var settings = new ShellSettings(new("TestShell"));
        var shellContext = new ShellContext(settings, serviceProvider);
        var factory = new DefaultShellContextScopeFactory();

        // Act
        using var scope = factory.CreateScope(shellContext);

        // Assert
        Assert.NotNull(scope);
        Assert.Same(shellContext, scope.ShellContext);
    }

    [Fact(DisplayName = "CreateScope returns scope with valid ServiceProvider")]
    public void CreateScope_WithValidShellContext_ReturnsScopeWithValidServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedService>();
        using var serviceProvider = services.BuildServiceProvider();
        var settings = new ShellSettings(new("TestShell"));
        var shellContext = new ShellContext(settings, serviceProvider);
        var factory = new DefaultShellContextScopeFactory();

        // Act
        using var scope = factory.CreateScope(shellContext);

        // Assert
        Assert.NotNull(scope.ServiceProvider);
        var service = scope.ServiceProvider.GetService<IScopedService>();
        Assert.NotNull(service);
    }

    [Fact(DisplayName = "Scope Dispose disposes underlying service scope")]
    public void Scope_Dispose_DisposesUnderlyingServiceScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<DisposableService>();
        using var serviceProvider = services.BuildServiceProvider();
        var settings = new ShellSettings(new("TestShell"));
        var shellContext = new ShellContext(settings, serviceProvider);
        var factory = new DefaultShellContextScopeFactory();

        DisposableService? disposableService;

        // Act
        var scope = factory.CreateScope(shellContext);
        disposableService = scope.ServiceProvider.GetRequiredService<DisposableService>();
        Assert.False(disposableService.IsDisposed);

        scope.Dispose();

        // Assert
        Assert.True(disposableService.IsDisposed);
    }

    [Fact(DisplayName = "Multiple scopes from same ShellContext are independent")]
    public void MultipleScopes_FromSameShellContext_AreIndependent()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedService>();
        using var serviceProvider = services.BuildServiceProvider();
        var settings = new ShellSettings(new("TestShell"));
        var shellContext = new ShellContext(settings, serviceProvider);
        var factory = new DefaultShellContextScopeFactory();

        // Act
        using var scope1 = factory.CreateScope(shellContext);
        using var scope2 = factory.CreateScope(shellContext);

        var service1 = scope1.ServiceProvider.GetRequiredService<IScopedService>();
        var service2 = scope2.ServiceProvider.GetRequiredService<IScopedService>();

        // Assert - Different scopes should have different scoped service instances
        Assert.NotSame(service1, service2);
    }

    [Fact(DisplayName = "Scope can be disposed multiple times without error")]
    public void Scope_DisposedMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        using var serviceProvider = services.BuildServiceProvider();
        var settings = new ShellSettings(new("TestShell"));
        var shellContext = new ShellContext(settings, serviceProvider);
        var factory = new DefaultShellContextScopeFactory();

        var scope = factory.CreateScope(shellContext);

        // Act & Assert - Should not throw
        scope.Dispose();
        scope.Dispose();
    }

    // Helper interfaces and classes for testing
    private interface IScopedService { }
    private class ScopedService : IScopedService { }

    private class DisposableService : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
