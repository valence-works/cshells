using CShells.AspNetCore.Middleware;
using CShells.Hosting;
using CShells.Resolution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for <see cref="ShellMiddleware"/>.
/// </summary>
public class ShellMiddlewareTests
{
    private static ShellMiddleware CreateMiddleware(
        RequestDelegate next,
        IShellResolver? resolver = null,
        IShellHost? host = null,
        IMemoryCache? cache = null,
        IOptions<ShellMiddlewareOptions>? options = null)
    {
        cache ??= new MemoryCache(new MemoryCacheOptions());
        options ??= Options.Create(new ShellMiddlewareOptions());
        return new(next, resolver ?? new NullShellResolver(), host ?? new TestShellHost(), cache, options);
    }

    [Fact(DisplayName = "InvokeAsync with no shells registered continues without setting scope")]
    public async Task InvokeAsync_WithNoShellsRegistered_ContinuesWithoutSettingScope()
    {
        // Arrange
        var originalServiceProvider = new ServiceCollection().BuildServiceProvider();
        var resolver = new NullShellResolver();
        var host = new TestShellHost(); // Empty host with no shells
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

    [Fact(DisplayName = "InvokeAsync with null ShellId continues without setting scope")]
    public async Task InvokeAsync_WithNullShellId_ContinuesWithoutSettingScope()
    {
        // Arrange
        var originalServiceProvider = new ServiceCollection().BuildServiceProvider();
        var shellServices = new ServiceCollection();
        var shellServiceProvider = shellServices.BuildServiceProvider();

        var settings = new ShellSettings(new("TestShell"));
        var shellContext = new ShellContext(settings, shellServiceProvider, Array.Empty<string>());

        var resolver = new NullShellResolver();
        var host = new TestShellHost(shellContext); // Host with a shell, but resolver returns null
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
        var shellContext = new ShellContext(settings, shellServiceProvider, Array.Empty<string>());

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

    [Fact(DisplayName = "InvokeAsync sets RequestServices to shell scope for the request lifetime")]
    public async Task InvokeAsync_SetsRequestServicesToShellScope_ForRequestLifetime()
    {
        // Arrange
        var originalServiceProvider = new ServiceCollection().BuildServiceProvider();
        var shellServices = new ServiceCollection();
        shellServices.AddSingleton<ITestService, TestService>();
        var shellServiceProvider = shellServices.BuildServiceProvider();

        var settings = new ShellSettings(new("TestShell"));
        var shellContext = new ShellContext(settings, shellServiceProvider, Array.Empty<string>());

        var resolver = new FixedShellResolver(new("TestShell"));
        var host = new TestShellHost(shellContext);

        var middleware = CreateMiddleware(ctx => Task.CompletedTask, resolver, host);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = originalServiceProvider
        };

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert - RequestServices should remain set to shell scope (needed for endpoints)
        Assert.NotSame(originalServiceProvider, httpContext.RequestServices);
        Assert.NotNull(httpContext.RequestServices.GetService<ITestService>());
    }

    [Fact(DisplayName = "InvokeAsync disposes shell scope even after exception")]
    public async Task InvokeAsync_DisposesShellScope_EvenAfterException()
    {
        // Arrange
        var originalServiceProvider = new ServiceCollection().BuildServiceProvider();
        var shellServices = new ServiceCollection();
        var shellServiceProvider = shellServices.BuildServiceProvider();

        var settings = new ShellSettings(new("TestShell"));
        var shellContext = new ShellContext(settings, shellServiceProvider, Array.Empty<string>());

        var resolver = new FixedShellResolver(new("TestShell"));
        var host = new TestShellHost(shellContext);

        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Test exception"), resolver, host);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = originalServiceProvider
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(httpContext));
        // The scope should be disposed even if an exception occurs
        // Note: We can't directly test disposal, but the test verifies the exception is properly propagated
    }

    [Theory(DisplayName = "Constructor guard clauses throw ArgumentNullException")]
    [InlineData(true, false, false, false, false, "next")]
    [InlineData(false, true, false, false, false, "resolver")]
    [InlineData(false, false, true, false, false, "host")]
    [InlineData(false, false, false, true, false, "cache")]
    [InlineData(false, false, false, false, true, "options")]
    public void Constructor_GuardClauses_ThrowArgumentNullException(bool nullNext, bool nullResolver, bool nullHost, bool nullCache, bool nullOptions, string expectedParam)
    {
        RequestDelegate? next = nullNext ? null : _ => Task.CompletedTask;
        var resolver = nullResolver ? null : new NullShellResolver();
        var host = nullHost ? null : new TestShellHost();
        var cache = nullCache ? null : new MemoryCache(new MemoryCacheOptions());
        var options = nullOptions ? null : Options.Create(new ShellMiddlewareOptions());

        var exception = Assert.Throws<ArgumentNullException>(() => new ShellMiddleware(next!, resolver!, host!, cache!, options!));
        Assert.Equal(expectedParam, exception.ParamName);
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
