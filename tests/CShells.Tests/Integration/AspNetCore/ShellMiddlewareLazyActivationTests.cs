using CShells.AspNetCore.Extensions;
using CShells.AspNetCore.Middleware;
using CShells.AspNetCore.Routing;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using CShells.Lifecycle.Policies;
using CShells.Resolution;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Feature-007 US4 tests: middleware translates registry exceptions into HTTP responses and
/// performs lazy activation on first touch.
/// </summary>
public class ShellMiddlewareLazyActivationTests
{
    [Fact(DisplayName = "Request for an unknown shell name → 404")]
    public async Task Request_UnknownShell_Returns404()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            new FixedShellResolver("unknown"),
            new ThrowingRegistry(new ShellBlueprintNotFoundException("unknown")));

        var ctx = new DefaultHttpContext();
        ctx.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());

        await middleware.InvokeAsync(ctx);

        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.False(nextCalled, "Next delegate should NOT be invoked on 404.");
    }

    [Fact(DisplayName = "Request when provider unavailable → 503")]
    public async Task Request_ProviderUnavailable_Returns503()
    {
        var nextCalled = false;
        var inner = new InvalidOperationException("db down");
        var middleware = CreateMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            new FixedShellResolver("flaky"),
            new ThrowingRegistry(new ShellBlueprintUnavailableException("flaky", inner)));

        var ctx = new DefaultHttpContext();
        ctx.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());

        await middleware.InvokeAsync(ctx);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, ctx.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact(DisplayName = "Successful GetOrActivateAsync sets RequestServices and invokes next")]
    public async Task Request_ActivationSucceeds_ChainsToNext()
    {
        var nextCalled = false;
        var shell = ShellMiddlewareTests.FakeShell.WithServices(_ => { }, name: "acme");
        var registry = new ShellMiddlewareTests.FakeRegistry(shell);

        var middleware = CreateMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            new FixedShellResolver("acme"),
            registry);

        var ctx = new DefaultHttpContext();
        ctx.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());

        await middleware.InvokeAsync(ctx);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.NotEqual(StatusCodes.Status503ServiceUnavailable, ctx.Response.StatusCode);
    }

    [Fact(DisplayName = "Cold-start re-match honours inline route constraints")]
    public async Task ColdStart_ReMatch_HonoursInlineConstraints()
    {
        // Two endpoints whose raw templates are structurally identical but disambiguated
        // by inline constraint. TemplateMatcher alone matches both structurally, so without
        // constraint evaluation the first-added endpoint would win regardless of whether
        // the route value satisfies its policy. The fix walks RoutePattern.ParameterPolicies
        // and rejects candidates whose IRouteConstraints don't accept the route values.
        var dataSource = new DynamicShellEndpointDataSource();

        var settings = new ShellSettings();
        var shellId = new ShellId("acme");
        var shellMetadata = new ShellEndpointMetadata(shellId, 1, settings);

        // Add the int-constrained endpoint FIRST, so structural-only matching would pick it.
        var intHandlerInvoked = false;
        var intEndpoint = new RouteEndpoint(
            _ => { intHandlerInvoked = true; return Task.CompletedTask; },
            RoutePatternFactory.Parse("acme/orders/{id:int}"),
            order: 0,
            new EndpointMetadataCollection(shellMetadata),
            displayName: "int-handler");

        var alphaHandlerInvoked = false;
        var alphaEndpoint = new RouteEndpoint(
            _ => { alphaHandlerInvoked = true; return Task.CompletedTask; },
            RoutePatternFactory.Parse("acme/orders/{slug:alpha}"),
            order: 0,
            new EndpointMetadataCollection(shellMetadata),
            displayName: "alpha-handler");

        dataSource.AddEndpoints([intEndpoint, alphaEndpoint]);

        var shell = ShellMiddlewareTests.FakeShell.WithServices(_ => { }, name: "acme");

        var middleware = CreateMiddleware(
            ctx => ctx.GetEndpoint() is { } ep ? ((RouteEndpoint)ep).RequestDelegate!(ctx) : Task.CompletedTask,
            new FixedShellResolver("acme"),
            new ColdActivatingRegistry(shell),
            dataSource);

        var ctx = new DefaultHttpContext();
        ctx.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        ctx.Request.Path = "/acme/orders/hello";
        // AddRouting registers IInlineConstraintResolver — needed for the constraint-resolution
        // fallback when RoutePatternFactory.Parse didn't pre-resolve policies on the reference.
        ctx.RequestServices = new ServiceCollection().AddRouting().BuildServiceProvider();

        await middleware.InvokeAsync(ctx);

        Assert.Equal("alpha-handler", ctx.GetEndpoint()?.DisplayName);
        Assert.True(alphaHandlerInvoked);
        Assert.False(intHandlerInvoked, "int-handler must be rejected by the inline `:int` constraint.");
    }

    [Fact(DisplayName = "Cold-start re-match replaces non-shell fallback endpoint")]
    public async Task ColdStart_ReMatch_ReplacesFallbackEndpoint()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("Default");
        var settings = new ShellSettings();
        var shellMetadata = new ShellEndpointMetadata(shellId, 1, settings);

        var shellEndpointInvoked = false;
        var shellEndpoint = new RouteEndpoint(
            _ => { shellEndpointInvoked = true; return Task.CompletedTask; },
            RoutePatternFactory.Parse("elsa/api/package/version"),
            order: 0,
            new EndpointMetadataCollection(shellMetadata, new HttpMethodMetadata(["GET"])),
            displayName: "package-version");
        dataSource.AddEndpoints([shellEndpoint]);

        var fallbackInvoked = false;
        var fallbackEndpoint = new RouteEndpoint(
            _ => { fallbackInvoked = true; return Task.CompletedTask; },
            RoutePatternFactory.Parse("{*path:nonfile}"),
            order: int.MaxValue,
            EndpointMetadataCollection.Empty,
            displayName: "spa-fallback");

        var shell = ShellMiddlewareTests.FakeShell.WithServices(_ => { }, name: "Default");
        var middleware = CreateMiddleware(
            ctx => ctx.GetEndpoint() is { } endpoint ? ((RouteEndpoint)endpoint).RequestDelegate!(ctx) : Task.CompletedTask,
            new FixedShellResolver("Default"),
            new ColdActivatingRegistry(shell),
            dataSource);

        var ctx = new DefaultHttpContext();
        ctx.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/elsa/api/package/version";
        ctx.SetEndpoint(fallbackEndpoint);
        ctx.RequestServices = new ServiceCollection().AddRouting().BuildServiceProvider();

        await middleware.InvokeAsync(ctx);

        Assert.Equal("package-version", ctx.GetEndpoint()?.DisplayName);
        Assert.True(shellEndpointInvoked);
        Assert.False(fallbackInvoked);
    }

    [Fact(DisplayName = "Cold re-match leases the generation before exposing its endpoint to a concurrent reload")]
    public async Task ColdStart_ReMatch_ConcurrentReload_KeepsMatchedGenerationLeased()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShellsAspNetCore(cshells => cshells
            .WithAssemblyContaining<RollbackStableFeature>()
            .AddShell("cold-race", shell => shell.WithFeature<RollbackStableFeature>())
            .ConfigureDrainPolicy(new FixedTimeoutDrainPolicy(TimeSpan.FromSeconds(5))));

        await using var host = services.BuildServiceProvider();
        host.GetRequiredService<EndpointRouteBuilderAccessor>().EndpointRouteBuilder =
            new TestEndpointRouteBuilder(host);
        host.GetRequiredService<ApplicationBuilderAccessor>().ApplicationBuilder =
            new ApplicationBuilder(host);
        var registry = host.GetRequiredService<IShellRegistry>();
        var response = new ShellMiddlewareTests.FireableResponseFeature();
        var endpointFeature = new BlockingShellEndpointFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(response);
        context.Features.Set<IEndpointFeature>(endpointFeature);
        context.Request.Method = "GET";
        context.Request.Path = "/rollback";
        var endpointInvoked = false;
        var middleware = CreateMiddleware(
            _ =>
            {
                endpointInvoked = true;
                return Task.CompletedTask;
            },
            new FixedShellResolver("cold-race"),
            registry,
            host.GetRequiredService<DynamicShellEndpointDataSource>());

        var request = Task.Run(() => middleware.InvokeAsync(context));
        await endpointFeature.ShellEndpointAssigned.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var generationOne = registry.GetActive("cold-race")!;
            var reload = await registry.ReloadAsync("cold-race");

            Assert.NotNull(reload.Drain);
            await Assert.ThrowsAsync<TimeoutException>(() =>
                reload.Drain!.WaitAsync().WaitAsync(TimeSpan.FromMilliseconds(100)));
            Assert.Equal(1, ((Shell)generationOne).ActiveScopeCount);

            endpointFeature.Release();
            await request;
            Assert.True(endpointInvoked);
            await response.FireOnCompletedAsync();
            await reload.Drain!.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(ShellLifecycleState.Disposed, generationOne.State);
        }
        finally
        {
            endpointFeature.Release();
            try
            {
                await request.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Preserve the primary assertion failure while ensuring the blocked worker exits.
            }
            await response.FireOnCompletedAsync();
        }
    }

    [Fact(DisplayName = "Cold-start re-match preserves fallback endpoint when no shell endpoint matches")]
    public async Task ColdStart_ReMatch_PreservesFallbackEndpoint_WhenNoShellEndpointMatches()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("Default");
        var settings = new ShellSettings();
        var shellMetadata = new ShellEndpointMetadata(shellId, 1, settings);

        var shellEndpointInvoked = false;
        var shellEndpoint = new RouteEndpoint(
            _ => { shellEndpointInvoked = true; return Task.CompletedTask; },
            RoutePatternFactory.Parse("elsa/api/package/version"),
            order: 0,
            new EndpointMetadataCollection(shellMetadata, new HttpMethodMetadata(["GET"])),
            displayName: "package-version");
        dataSource.AddEndpoints([shellEndpoint]);

        var fallbackInvoked = false;
        var fallbackEndpoint = new RouteEndpoint(
            _ => { fallbackInvoked = true; return Task.CompletedTask; },
            RoutePatternFactory.Parse("{*path:nonfile}"),
            order: int.MaxValue,
            EndpointMetadataCollection.Empty,
            displayName: "spa-fallback");

        var shell = ShellMiddlewareTests.FakeShell.WithServices(_ => { }, name: "Default");
        var middleware = CreateMiddleware(
            ctx => ctx.GetEndpoint() is { } endpoint ? ((RouteEndpoint)endpoint).RequestDelegate!(ctx) : Task.CompletedTask,
            new FixedShellResolver("Default"),
            new ColdActivatingRegistry(shell),
            dataSource);

        var ctx = new DefaultHttpContext();
        ctx.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/does/not/match";
        ctx.SetEndpoint(fallbackEndpoint);
        ctx.RequestServices = new ServiceCollection().AddRouting().BuildServiceProvider();

        await middleware.InvokeAsync(ctx);

        Assert.Equal("spa-fallback", ctx.GetEndpoint()?.DisplayName);
        Assert.True(fallbackInvoked);
        Assert.False(shellEndpointInvoked);
    }

    [Fact(DisplayName = "Cold-start re-match preserves non-shell host endpoint")]
    public async Task ColdStart_ReMatch_PreservesHostEndpoint()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("Default");
        var settings = new ShellSettings();
        var shellMetadata = new ShellEndpointMetadata(shellId, 1, settings);

        var shellEndpointInvoked = false;
        var shellEndpoint = new RouteEndpoint(
            _ => { shellEndpointInvoked = true; return Task.CompletedTask; },
            RoutePatternFactory.Parse("host/status"),
            order: 0,
            new EndpointMetadataCollection(shellMetadata, new HttpMethodMetadata(["GET"])),
            displayName: "shell-status");
        dataSource.AddEndpoints([shellEndpoint]);

        var hostEndpointInvoked = false;
        var hostEndpoint = new RouteEndpoint(
            _ => { hostEndpointInvoked = true; return Task.CompletedTask; },
            RoutePatternFactory.Parse("host/status"),
            order: 0,
            new EndpointMetadataCollection(new HttpMethodMetadata(["GET"])),
            displayName: "host-status");

        var shell = ShellMiddlewareTests.FakeShell.WithServices(_ => { }, name: "Default");
        var middleware = CreateMiddleware(
            ctx => ctx.GetEndpoint() is { } endpoint ? ((RouteEndpoint)endpoint).RequestDelegate!(ctx) : Task.CompletedTask,
            new FixedShellResolver("Default"),
            new ColdActivatingRegistry(shell),
            dataSource);

        var ctx = new DefaultHttpContext();
        ctx.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/host/status";
        ctx.SetEndpoint(hostEndpoint);
        ctx.RequestServices = new ServiceCollection().AddRouting().BuildServiceProvider();

        await middleware.InvokeAsync(ctx);

        Assert.Equal("host-status", ctx.GetEndpoint()?.DisplayName);
        Assert.True(hostEndpointInvoked);
        Assert.False(shellEndpointInvoked);
    }

    // =================================================================
    // Test doubles
    // =================================================================

    private static ShellMiddleware CreateMiddleware(
        RequestDelegate next,
        IShellResolver resolver,
        IShellRegistry registry,
        DynamicShellEndpointDataSource? endpointDataSource = null) =>
        ShellMiddlewareTests.CreateMiddleware(next, resolver, registry, endpointDataSource);

    private sealed class FixedShellResolver(string name) : IShellResolver
    {
        public Task<ShellId?> ResolveAsync(ShellResolutionContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult<ShellId?>(new ShellId(name));
    }

    private sealed class BlockingShellEndpointFeature : IEndpointFeature
    {
        private readonly TaskCompletionSource assigned = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Endpoint? endpoint;

        public Task ShellEndpointAssigned => assigned.Task;

        public Endpoint? Endpoint
        {
            get => endpoint;
            set
            {
                endpoint = value;
                if (value?.Metadata.GetMetadata<ShellEndpointMetadata>() is null)
                    return;

                assigned.TrySetResult();
                release.Task.GetAwaiter().GetResult();
            }
        }

        public void Release() => release.TrySetResult();
    }

    private sealed class TestEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = [];
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }

    /// <summary>
    /// Registry whose <see cref="GetActive"/> always returns <c>null</c> so the middleware
    /// observes a cold activation, while <see cref="GetOrActivateAsync"/> returns a preset
    /// shell. Used to exercise the cold-start re-match path. The optional
    /// <paramref name="onActivate"/> callback simulates the inline lifecycle-subscriber work
    /// (endpoint + middleware-pipeline registration) that happens during real activation.
    /// </summary>
    internal sealed class ColdActivatingRegistry(IShell shell, Action? onActivate = null) : IShellRegistry
    {
        public Task<IShell> GetOrActivateAsync(string name, CancellationToken ct = default)
        {
            onActivate?.Invoke();
            return Task.FromResult(shell);
        }
        public Task<IShell> ActivateAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ReloadResult> ReloadAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReloadResult>> ReloadActiveAsync(ReloadOptions? options = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IDrainOperation> DrainAsync(IShell shell, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UnregisterBlueprintAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProvidedBlueprint?> GetBlueprintAsync(string name, CancellationToken ct = default) => Task.FromResult<ProvidedBlueprint?>(null);
        public Task<IShellBlueprintManager?> GetManagerAsync(string name, CancellationToken ct = default) => Task.FromResult<IShellBlueprintManager?>(null);
        public Task<ShellPage> ListAsync(ShellListQuery query, CancellationToken ct = default) => Task.FromResult(new ShellPage([], null));
        public IShell? GetActive(string name) => null;
        public IReadOnlyCollection<IShell> GetAll(string name) => [shell];
        public IReadOnlyCollection<IShell> GetActiveShells() => [shell];
        public void Subscribe(IShellLifecycleSubscriber subscriber) { }
        public void Unsubscribe(IShellLifecycleSubscriber subscriber) { }
    }

    /// <summary>Minimal registry that throws a preset exception from GetOrActivateAsync.</summary>
    private sealed class ThrowingRegistry(Exception toThrow) : IShellRegistry
    {
        public Task<IShell> GetOrActivateAsync(string name, CancellationToken ct = default) =>
            Task.FromException<IShell>(toThrow);
        public Task<IShell> ActivateAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ReloadResult> ReloadAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReloadResult>> ReloadActiveAsync(ReloadOptions? options = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IDrainOperation> DrainAsync(IShell shell, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UnregisterBlueprintAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProvidedBlueprint?> GetBlueprintAsync(string name, CancellationToken ct = default) => Task.FromResult<ProvidedBlueprint?>(null);
        public Task<IShellBlueprintManager?> GetManagerAsync(string name, CancellationToken ct = default) => Task.FromResult<IShellBlueprintManager?>(null);
        public Task<ShellPage> ListAsync(ShellListQuery query, CancellationToken ct = default) => Task.FromResult(new ShellPage([], null));
        public IShell? GetActive(string name) => null;
        public IReadOnlyCollection<IShell> GetAll(string name) => [];
        public IReadOnlyCollection<IShell> GetActiveShells() => [];
        public void Subscribe(IShellLifecycleSubscriber subscriber) { }
        public void Unsubscribe(IShellLifecycleSubscriber subscriber) { }
    }
}
