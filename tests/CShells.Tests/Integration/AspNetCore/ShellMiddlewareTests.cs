using CShells.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for <see cref="ShellMiddleware"/>.
/// </summary>
public class ShellMiddlewareTests
{
    [Fact(DisplayName = "InvokeAsync with null ShellId continues without setting scope")]
    public async Task InvokeAsync_WithNullShellId_ContinuesWithoutSettingScope()
    {
        // Arrange
        var originalServiceProvider = new ServiceCollection().BuildServiceProvider();
        var resolver = new NullShellResolver();
        var host = new TestShellHost();
        var nextCalled = false;

        var middleware = new ShellMiddleware(
            next: ctx =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            resolver: resolver,
            host: host);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = originalServiceProvider
        };

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.True(nextCalled);
        Assert.Same(originalServiceProvider, httpContext.RequestServices);
    }

    [Fact(DisplayName = "InvokeAsync with valid ShellId sets RequestServices from shell scope")]
    public async Task InvokeAsync_WithValidShellId_SetsRequestServicesFromShellScope()
    {
        // Arrange
        var originalServiceProvider = new ServiceCollection().BuildServiceProvider();
        var shellServices = new ServiceCollection();
        shellServices.AddSingleton<ITestService, TestService>();
        var shellServiceProvider = shellServices.BuildServiceProvider();

        var settings = new ShellSettings(new("TestShell"));
        var shellContext = new ShellContext(settings, shellServiceProvider);

        var resolver = new FixedShellResolver(new("TestShell"));
        var host = new TestShellHost(shellContext);

        IServiceProvider? capturedRequestServices = null;
        ITestService? capturedTestService = null;
        var middleware = new ShellMiddleware(
            next: ctx =>
            {
                capturedRequestServices = ctx.RequestServices;
                // Capture the service while within the scope
                capturedTestService = ctx.RequestServices.GetService<ITestService>();
                return Task.CompletedTask;
            },
            resolver: resolver,
            host: host);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = originalServiceProvider
        };

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.NotNull(capturedRequestServices);
        Assert.NotSame(originalServiceProvider, capturedRequestServices);

        // Verify services from shell are available (captured while in scope)
        Assert.NotNull(capturedTestService);
    }

    [Fact(DisplayName = "InvokeAsync restores original RequestServices after completion")]
    public async Task InvokeAsync_AfterCompletion_RestoresOriginalRequestServices()
    {
        // Arrange
        var originalServiceProvider = new ServiceCollection().BuildServiceProvider();
        var shellServices = new ServiceCollection();
        var shellServiceProvider = shellServices.BuildServiceProvider();

        var settings = new ShellSettings(new("TestShell"));
        var shellContext = new ShellContext(settings, shellServiceProvider);

        var resolver = new FixedShellResolver(new("TestShell"));
        var host = new TestShellHost(shellContext);

        var middleware = new ShellMiddleware(
            next: ctx => Task.CompletedTask,
            resolver: resolver,
            host: host);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = originalServiceProvider
        };

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Same(originalServiceProvider, httpContext.RequestServices);
    }

    [Fact(DisplayName = "InvokeAsync restores original RequestServices even after exception")]
    public async Task InvokeAsync_AfterException_RestoresOriginalRequestServices()
    {
        // Arrange
        var originalServiceProvider = new ServiceCollection().BuildServiceProvider();
        var shellServices = new ServiceCollection();
        var shellServiceProvider = shellServices.BuildServiceProvider();

        var settings = new ShellSettings(new("TestShell"));
        var shellContext = new ShellContext(settings, shellServiceProvider);

        var resolver = new FixedShellResolver(new("TestShell"));
        var host = new TestShellHost(shellContext);

        var middleware = new ShellMiddleware(
            next: ctx => throw new InvalidOperationException("Test exception"),
            resolver: resolver,
            host: host);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = originalServiceProvider
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(httpContext));
        Assert.Same(originalServiceProvider, httpContext.RequestServices);
    }

    [Fact(DisplayName = "Constructor with null next throws ArgumentNullException")]
    public void Constructor_WithNullNext_ThrowsArgumentNullException()
    {
        // Arrange
        var resolver = new NullShellResolver();
        var host = new TestShellHost();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new ShellMiddleware(null!, resolver, host));
        Assert.Equal("next", ex.ParamName);
    }

    [Fact(DisplayName = "Constructor with null resolver throws ArgumentNullException")]
    public void Constructor_WithNullResolver_ThrowsArgumentNullException()
    {
        // Arrange
        RequestDelegate next = ctx => Task.CompletedTask;
        var host = new TestShellHost();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new ShellMiddleware(next, null!, host));
        Assert.Equal("resolver", ex.ParamName);
    }

    [Fact(DisplayName = "Constructor with null host throws ArgumentNullException")]
    public void Constructor_WithNullHost_ThrowsArgumentNullException()
    {
        // Arrange
        RequestDelegate next = ctx => Task.CompletedTask;
        var resolver = new NullShellResolver();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new ShellMiddleware(next, resolver, null!));
        Assert.Equal("host", ex.ParamName);
    }

    [Fact(DisplayName = "InvokeAsync with non-existent ShellId throws KeyNotFoundException")]
    public async Task InvokeAsync_WithNonExistentShellId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var resolver = new FixedShellResolver(new("NonExistent"));
        var host = new TestShellHost(); // Empty host with no shells
        var middleware = new ShellMiddleware(
            next: ctx => Task.CompletedTask,
            resolver: resolver,
            host: host);
        var httpContext = new DefaultHttpContext();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => middleware.InvokeAsync(httpContext));
    }

    // Test helpers
    private interface ITestService { }
    private class TestService : ITestService { }

    private class NullShellResolver : IShellResolver
    {
        public ShellId? Resolve(HttpContext httpContext) => null;
    }

    private class FixedShellResolver : IShellResolver
    {
        private readonly ShellId _shellId;
        public FixedShellResolver(ShellId shellId) => _shellId = shellId;
        public ShellId? Resolve(HttpContext httpContext) => _shellId;
    }

    private class TestShellHost : IShellHost
    {
        private readonly ShellContext? _shellContext;

        public TestShellHost(ShellContext? shellContext = null)
        {
            _shellContext = shellContext;
        }

        public ShellContext DefaultShell => _shellContext ?? throw new InvalidOperationException("No shell configured");
        public IReadOnlyCollection<ShellContext> AllShells => _shellContext != null ? [_shellContext] : [];

        public ShellContext GetShell(ShellId id)
        {
            if (_shellContext == null)
            {
                throw new KeyNotFoundException($"Shell '{id}' not found");
            }
            return _shellContext;
        }
    }
}
