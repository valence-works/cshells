using CShells.AspNetCore.Middleware;
using CShells.Hosting;
using CShells.Resolution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for <see cref="ShellMiddleware"/>.
/// </summary>
public class ShellMiddlewareTests
{
    private static ShellMiddleware CreateMiddleware(RequestDelegate next, IShellResolver? resolver = null, IShellHost? host = null)
    {
        return new(next, resolver ?? new NullShellResolver(), host ?? new TestShellHost());
    }

    [Fact(DisplayName = "InvokeAsync with null ShellId continues without setting scope")]
    public async Task InvokeAsync_WithNullShellId_ContinuesWithoutSettingScope()
    {
        // Arrange
        var originalServiceProvider = new ServiceCollection().BuildServiceProvider();
        var resolver = new NullShellResolver();
        var host = new TestShellHost();
        var nextCalled = false;

        var middleware = CreateMiddleware(
            ctx =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            resolver,
            host);

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
        var middleware = CreateMiddleware(
            ctx =>
            {
                capturedRequestServices = ctx.RequestServices;
                // Capture the service while within the scope
                capturedTestService = ctx.RequestServices.GetService<ITestService>();
                return Task.CompletedTask;
            },
            resolver,
            host);

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

        var middleware = CreateMiddleware(ctx => Task.CompletedTask, resolver, host);

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

        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Test exception"), resolver, host);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = originalServiceProvider
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(httpContext));
        Assert.Same(originalServiceProvider, httpContext.RequestServices);
    }

    [Theory(DisplayName = "Constructor guard clauses throw ArgumentNullException")]
    [InlineData(true, false, false, "next")]
    [InlineData(false, true, false, "resolver")]
    [InlineData(false, false, true, "host")]
    public void Constructor_GuardClauses_ThrowArgumentNullException(bool nullNext, bool nullResolver, bool nullHost, string expectedParam)
    {
        RequestDelegate? next = nullNext ? null : _ => Task.CompletedTask;
        var resolver = nullResolver ? null : new NullShellResolver();
        var host = nullHost ? null : new TestShellHost();

        var exception = Assert.Throws<ArgumentNullException>(() => new ShellMiddleware(next!, resolver!, host!));
        Assert.Equal(expectedParam, exception.ParamName);
    }

    [Fact(DisplayName = "InvokeAsync with non-existent ShellId throws KeyNotFoundException")]
    public async Task InvokeAsync_WithNonExistentShellId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var resolver = new FixedShellResolver(new("NonExistent"));
        var host = new TestShellHost(); // Empty host with no shells
        var middleware = CreateMiddleware(ctx => Task.CompletedTask, resolver, host);
        var httpContext = new DefaultHttpContext();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => middleware.InvokeAsync(httpContext));
    }

    // Test helpers
    private interface ITestService { }
    private class TestService : ITestService { }

    private class NullShellResolver : IShellResolver
    {
        public ShellId? Resolve(ShellResolutionContext context) => null;
    }

    private class FixedShellResolver(ShellId shellId) : IShellResolver
    {
        public ShellId? Resolve(ShellResolutionContext context) => shellId;
    }

    private class TestShellHost(ShellContext? shellContext = null) : IShellHost
    {
        public ShellContext DefaultShell => shellContext ?? throw new InvalidOperationException("No shell configured");
        public IReadOnlyCollection<ShellContext> AllShells => shellContext != null ? [shellContext] : [];

        public ShellContext GetShell(ShellId id)
        {
            if (shellContext == null)
            {
                throw new KeyNotFoundException($"Shell '{id}' not found");
            }
            return shellContext;
        }
    }
}
