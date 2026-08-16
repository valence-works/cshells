using CShells.AspNetCore.Middleware;
using CShells.AspNetCore.Routing;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using CShells.Lifecycle.Policies;
using CShells.Resolution;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for <see cref="ShellMiddleware"/> — the middleware that resolves a shell per request
/// and sets <see cref="HttpContext.RequestServices"/> to a scope from that shell's provider.
/// The scope is released via <see cref="HttpResponse.OnCompleted"/> so upstream middleware can
/// still read RequestServices during post-_next processing.
/// </summary>
public class ShellMiddlewareTests
{
    [Fact(DisplayName = "InvokeAsync with no shells registered continues without setting scope")]
    public async Task InvokeAsync_NoShellsRegistered_ContinuesWithoutSettingScope()
    {
        var originalServiceProvider = new ServiceCollection().BuildServiceProvider();
        var nextCalled = false;

        var middleware = CreateMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            registry: new FakeRegistry());

        var httpContext = new DefaultHttpContext { RequestServices = originalServiceProvider };

        await middleware.InvokeAsync(httpContext);

        Assert.True(nextCalled);
        Assert.Same(originalServiceProvider, httpContext.RequestServices);
    }

    [Fact(DisplayName = "InvokeAsync with null resolved ShellId continues without setting scope")]
    public async Task InvokeAsync_NullResolvedId_ContinuesWithoutSettingScope()
    {
        var originalServiceProvider = new ServiceCollection().BuildServiceProvider();
        var shell = FakeShell.WithServices(_ => { });
        var registry = new FakeRegistry(shell);

        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            resolver: new NullShellResolver(),
            registry: registry);

        var httpContext = new DefaultHttpContext { RequestServices = originalServiceProvider };

        await middleware.InvokeAsync(httpContext);

        Assert.Same(originalServiceProvider, httpContext.RequestServices);
    }

    [Fact(DisplayName = "InvokeAsync with a resolved shell sets RequestServices to a shell scope")]
    public async Task InvokeAsync_ValidShell_SetsRequestServices_FromShellScope()
    {
        var originalServiceProvider = new ServiceCollection().BuildServiceProvider();
        var shell = FakeShell.WithServices(s => s.AddSingleton<ITestService, TestService>(), name: "TestShell");

        IServiceProvider? capturedRequestServices = null;
        ITestService? capturedService = null;

        var middleware = CreateMiddleware(
            ctx =>
            {
                capturedRequestServices = ctx.RequestServices;
                capturedService = ctx.RequestServices.GetService<ITestService>();
                return Task.CompletedTask;
            },
            resolver: new FixedShellResolver("TestShell"),
            registry: new FakeRegistry(shell));

        var (httpContext, responseFeature) = CreateHttpContextWithFireableResponse(originalServiceProvider);

        await middleware.InvokeAsync(httpContext);

        Assert.NotNull(capturedRequestServices);
        Assert.NotSame(originalServiceProvider, capturedRequestServices);
        Assert.NotNull(capturedService);

        // Fire OnCompleted so the scope releases, then verify the counter dropped to zero.
        await responseFeature.FireOnCompletedAsync();
        Assert.Equal(0, shell.ActiveScopeCount);
    }

    [Fact(DisplayName = "Scope is held during _next and released via Response.OnCompleted, not at InvokeAsync return")]
    public async Task Scope_HeldDuringRequest_ReleasedAtResponseCompletion()
    {
        var shell = FakeShell.WithServices(_ => { }, name: "TestShell");
        var registry = new FakeRegistry(shell);

        int activeScopesDuringNext = -1;
        var middleware = CreateMiddleware(
            _ => { activeScopesDuringNext = shell.ActiveScopeCount; return Task.CompletedTask; },
            resolver: new FixedShellResolver("TestShell"),
            registry: registry);

        var (httpContext, responseFeature) = CreateHttpContextWithFireableResponse();

        await middleware.InvokeAsync(httpContext);

        // Scope was active during _next.
        Assert.Equal(1, activeScopesDuringNext);

        // Scope is STILL held after InvokeAsync returns — deferred to OnCompleted so upstream
        // middleware can read RequestServices during its post-_next work.
        Assert.Equal(1, shell.ActiveScopeCount);

        // Simulate the server firing OnCompleted callbacks after the response is written.
        await responseFeature.FireOnCompletedAsync();

        Assert.Equal(0, shell.ActiveScopeCount);
    }

    [Fact(DisplayName = "Downstream exception propagates; scope is released when OnCompleted fires on error paths")]
    public async Task DownstreamException_Propagates()
    {
        var shell = FakeShell.WithServices(_ => { }, name: "TestShell");
        var middleware = CreateMiddleware(
            _ => throw new InvalidOperationException("boom"),
            resolver: new FixedShellResolver("TestShell"),
            registry: new FakeRegistry(shell));

        var (httpContext, responseFeature) = CreateHttpContextWithFireableResponse();

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(httpContext));

        // In a real server, OnCompleted fires even on error paths once the response is finalized.
        // Simulate that here; the scope should release cleanly.
        await responseFeature.FireOnCompletedAsync();
        Assert.Equal(0, shell.ActiveScopeCount);
    }

    [Fact(DisplayName = "InvokeAsync with a registered shell pipeline dispatches through it, then rejoins next")]
    public async Task InvokeAsync_PipelineRegistered_DispatchesThroughShellPipeline()
    {
        var shell = FakeShell.WithServices(s => s.AddSingleton<ITestService, TestService>(), name: "TestShell");
        var pipelineRegistry = new ShellMiddlewarePipelineRegistry();
        var executionOrder = new List<string>();
        IServiceProvider? servicesInsidePipeline = null;

        var continuation = new ShellPipelineContinuation();
        pipelineRegistry.Set(new ShellId("TestShell"), shell.Descriptor.Generation, ctx =>
        {
            executionOrder.Add("pipeline");
            servicesInsidePipeline = ctx.RequestServices;
            return continuation.Next(ctx);
        }, continuation);

        var middleware = CreateMiddleware(
            _ => { executionOrder.Add("next"); return Task.CompletedTask; },
            resolver: new FixedShellResolver("TestShell"),
            registry: new FakeRegistry(shell),
            pipelineRegistry: pipelineRegistry);

        var (httpContext, _) = CreateHttpContextWithFireableResponse();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(["pipeline", "next"], executionOrder);
        Assert.NotNull(servicesInsidePipeline?.GetService<ITestService>()); // shell scope was already set
    }

    [Fact(DisplayName = "InvokeAsync binds a matched endpoint to its exact generation after reload")]
    public async Task InvokeAsync_MatchedOlderGeneration_UsesExactGeneration()
    {
        var generationOne = FakeShell.WithServices(_ => { }, name: "TestShell", generation: 1);
        var generationTwo = FakeShell.WithServices(_ => { }, name: "TestShell", generation: 2);
        var pipelineRegistry = new ShellMiddlewarePipelineRegistry();
        var generationOneInvoked = false;
        var generationTwoInvoked = false;

        pipelineRegistry.Set(new ShellId("TestShell"), 1, context =>
        {
            generationOneInvoked = true;
            return Task.CompletedTask;
        }, new ShellPipelineContinuation());
        pipelineRegistry.Set(new ShellId("TestShell"), 2, context =>
        {
            generationTwoInvoked = true;
            return Task.CompletedTask;
        }, new ShellPipelineContinuation());

        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            resolver: new FixedShellResolver("TestShell"),
            registry: new FakeRegistry(generationOne, generationTwo),
            pipelineRegistry: pipelineRegistry);

        var (httpContext, _) = CreateHttpContextWithFireableResponse();
        var settings = new ShellSettings(new ShellId("TestShell"));
        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("api/items"),
            order: 0,
            new EndpointMetadataCollection(
                new ShellEndpointMetadata(new ShellId("TestShell"), 1, settings)),
            "generation-one");
        httpContext.SetEndpoint(endpoint);

        await middleware.InvokeAsync(httpContext);

        Assert.True(generationOneInvoked);
        Assert.False(generationTwoInvoked);
    }

    [Fact(DisplayName = "Reload drain holds an old matched request while replacement requests use the new generation")]
    public async Task InvokeAsync_ReloadDrain_BindsInFlightRequestAndWaitsForCompletion()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShellsAspNetCore(cshells => cshells
            .WithAssemblies()
            .AddShell("payments", _ => { })
            .ConfigureDrainPolicy(new FixedTimeoutDrainPolicy(TimeSpan.FromSeconds(5))));

        await using var host = services.BuildServiceProvider();
        var registry = host.GetRequiredService<IShellRegistry>();
        var endpointDataSource = host.GetRequiredService<DynamicShellEndpointDataSource>();
        var pipelineRegistry = host.GetRequiredService<ShellMiddlewarePipelineRegistry>();
        var shellId = new ShellId("payments");
        var generationOne = await registry.ActivateAsync(shellId.Name);
        var settings = new ShellSettings(shellId);

        endpointDataSource.PublishGeneration(
            shellId,
            generationOne.Descriptor.Generation,
            [CreateEndpoint("/payments/old", shellId, generationOne.Descriptor.Generation, settings, "GenerationOne")]);
        var oldEndpoint = endpointDataSource.Endpoints.Single();

        var oldRequestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOldRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldPipelineInvoked = false;
        pipelineRegistry.Set(
            shellId,
            generationOne.Descriptor.Generation,
            async _ =>
            {
                oldPipelineInvoked = true;
                oldRequestEntered.SetResult();
                await releaseOldRequest.Task;
            },
            new ShellPipelineContinuation());

        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            resolver: new FixedShellResolver(shellId),
            registry: registry,
            endpointDataSource: endpointDataSource,
            pipelineRegistry: pipelineRegistry);

        var oldResponse = new FireableResponseFeature();
        var oldContext = new DefaultHttpContext();
        oldContext.Features.Set<IHttpResponseFeature>(oldResponse);
        oldContext.SetEndpoint(oldEndpoint);

        // Enter middleware before reload so this request's own generation-one scope is the
        // in-flight work that the real drain operation must wait for.
        var oldRequest = middleware.InvokeAsync(oldContext);
        await oldRequestEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(oldPipelineInvoked);

        var reload = await registry.ReloadAsync(shellId.Name);
        Assert.Null(reload.Error);
        Assert.NotNull(reload.NewShell);
        Assert.NotNull(reload.Drain);
        var generationTwo = reload.NewShell!;
        var oldDrain = reload.Drain!;
        Assert.Same(generationTwo, registry.GetActive(shellId.Name));

        endpointDataSource.PublishGeneration(
            shellId,
            generationTwo.Descriptor.Generation,
            [CreateEndpoint("/payments/new", shellId, generationTwo.Descriptor.Generation, settings, "GenerationTwo")]);

        var newRequestInvoked = false;
        pipelineRegistry.Set(
            shellId,
            generationTwo.Descriptor.Generation,
            _ =>
            {
                newRequestInvoked = true;
                return Task.CompletedTask;
            },
            new ShellPipelineContinuation());

        var newResponse = new FireableResponseFeature();
        var newContext = new DefaultHttpContext();
        newContext.Features.Set<IHttpResponseFeature>(newResponse);
        newContext.SetEndpoint(endpointDataSource.Endpoints.Single());

        await middleware.InvokeAsync(newContext);
        Assert.True(newRequestInvoked, "The replacement request must use generation two's pipeline.");
        await newResponse.FireOnCompletedAsync();

        // The old pipeline has returned, but its scope is deliberately retained by OnCompleted.
        releaseOldRequest.SetResult();
        await oldRequest;
        await Assert.ThrowsAsync<TimeoutException>(() => oldDrain.WaitAsync().WaitAsync(TimeSpan.FromMilliseconds(100)));

        await oldResponse.FireOnCompletedAsync();
        await oldDrain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ShellLifecycleState.Disposed, generationOne.State);
        Assert.Equal(generationTwo.Descriptor.Generation, endpointDataSource.Endpoints
            .Single()
            .Metadata.GetMetadata<ShellEndpointMetadata>()!
            .Generation);

        var replacementDrain = await registry.DrainAsync(generationTwo);
        await replacementDrain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "A request matched before reload keeps the exact generation leased until response completion")]
    public async Task InvokeAsync_MatchedBeforeReload_UsesRoutingLeaseAcrossMiddlewareGap()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShellsAspNetCore(cshells => cshells
            .WithAssemblies()
            .AddShell("leased", _ => { })
            .ConfigureDrainPolicy(new FixedTimeoutDrainPolicy(TimeSpan.FromSeconds(5))));

        await using var host = services.BuildServiceProvider();
        var registry = host.GetRequiredService<IShellRegistry>();
        var dataSource = host.GetRequiredService<DynamicShellEndpointDataSource>();
        var pipelines = host.GetRequiredService<ShellMiddlewarePipelineRegistry>();
        var policy = host.GetServices<MatcherPolicy>().OfType<ShellEndpointGenerationMatcherPolicy>().Single();
        var shellId = new ShellId("leased");
        var generationOne = await registry.ActivateAsync(shellId.Name);
        var settings = new ShellSettings(shellId);
        dataSource.PublishGeneration(
            shellId,
            generationOne.Descriptor.Generation,
            [CreateEndpoint("/leased/old", shellId, generationOne.Descriptor.Generation, settings, "GenerationOne")]);
        var oldEndpoint = dataSource.Endpoints.Single();
        var oldPipelineInvoked = false;
        pipelines.Set(
            shellId,
            generationOne.Descriptor.Generation,
            _ =>
            {
                oldPipelineInvoked = true;
                return Task.CompletedTask;
            },
            new ShellPipelineContinuation());
        var response = new FireableResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(response);
        var candidates = new CandidateSet([oldEndpoint], [new RouteValueDictionary()], [0]);

        await policy.ApplyAsync(context, candidates);
        await policy.ApplyAsync(context, candidates);
        context.SetEndpoint(oldEndpoint);

        Assert.Equal(int.MaxValue, policy.Order);
        Assert.Equal(1, ((Shell)generationOne).ActiveScopeCount);
        var reload = await registry.ReloadAsync(shellId.Name);
        Assert.NotNull(reload.Drain);
        await Assert.ThrowsAsync<TimeoutException>(() =>
            reload.Drain!.WaitAsync().WaitAsync(TimeSpan.FromMilliseconds(100)));

        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            registry: registry,
            endpointDataSource: dataSource,
            pipelineRegistry: pipelines);
        await middleware.InvokeAsync(context);

        Assert.True(oldPipelineInvoked);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        await Assert.ThrowsAsync<TimeoutException>(() =>
            reload.Drain!.WaitAsync().WaitAsync(TimeSpan.FromMilliseconds(100)));

        await response.FireOnCompletedAsync();
        await reload.Drain!.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ShellLifecycleState.Disposed, generationOne.State);
    }

    [Fact(DisplayName = "Routing lease acquisition is idempotent and releases when middleware is short-circuited")]
    public async Task MatcherPolicy_ReenteredThenShortCircuited_ReleasesSingleScopeOnCompletion()
    {
        var shell = FakeShell.WithServices(_ => { }, name: "leased", generation: 1);
        var policy = new ShellEndpointGenerationMatcherPolicy(new FakeRegistry(shell));
        var endpoint = CreateEndpoint(
            "/leased",
            new ShellId("leased"),
            generation: 1,
            new ShellSettings(new ShellId("leased")),
            "LeasedFeature");
        var response = new FireableResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(response);
        var candidates = new CandidateSet([endpoint], [new RouteValueDictionary()], [0]);

        await policy.ApplyAsync(context, candidates);
        await policy.ApplyAsync(context, candidates);

        Assert.Equal(1, shell.ActiveScopeCount);
        await response.FireOnCompletedAsync();
        Assert.Equal(0, shell.ActiveScopeCount);
    }

    [Fact(DisplayName = "Another shell's pipeline is not invoked for the resolved shell")]
    public async Task InvokeAsync_PipelineForDifferentShell_FallsBackToNext()
    {
        var shell = FakeShell.WithServices(_ => { }, name: "ShellB");
        var pipelineRegistry = new ShellMiddlewarePipelineRegistry();
        var shellAPipelineInvoked = false;
        var nextCalled = false;

        pipelineRegistry.Set(new ShellId("ShellA"), 1,
            _ => { shellAPipelineInvoked = true; return Task.CompletedTask; },
            new ShellPipelineContinuation());

        var middleware = CreateMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            resolver: new FixedShellResolver("ShellB"),
            registry: new FakeRegistry(shell),
            pipelineRegistry: pipelineRegistry);

        var (httpContext, _) = CreateHttpContextWithFireableResponse();

        await middleware.InvokeAsync(httpContext);

        Assert.True(nextCalled);
        Assert.False(shellAPipelineInvoked);
    }

    [Fact(DisplayName = "Pipeline registered during cold activation runs on the activating request")]
    public async Task InvokeAsync_ColdActivation_RegistersAndRunsPipelineOnFirstRequest()
    {
        // Mirrors the production guarantee: the registry awaits lifecycle subscribers inline
        // during activation, so the pipeline entry exists before GetOrActivateAsync returns.
        var shell = FakeShell.WithServices(_ => { }, name: "ColdShell");
        var pipelineRegistry = new ShellMiddlewarePipelineRegistry();
        var pipelineInvoked = false;

        var continuation = new ShellPipelineContinuation();
        var registry = new ShellMiddlewareLazyActivationTests.ColdActivatingRegistry(shell, onActivate: () =>
            pipelineRegistry.Set(new ShellId("ColdShell"), shell.Descriptor.Generation, ctx =>
            {
                pipelineInvoked = true;
                return continuation.Next(ctx);
            }, continuation));

        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            resolver: new FixedShellResolver("ColdShell"),
            registry: registry,
            pipelineRegistry: pipelineRegistry);

        var (httpContext, _) = CreateHttpContextWithFireableResponse();
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

        await middleware.InvokeAsync(httpContext);

        Assert.True(pipelineInvoked);
    }

    [Theory(DisplayName = "Constructor guard clauses throw ArgumentNullException")]
    [InlineData("next")]
    [InlineData("resolver")]
    [InlineData("registry")]
    [InlineData("endpointDataSource")]
    [InlineData("pipelineRegistry")]
    [InlineData("cache")]
    [InlineData("options")]
    public void Constructor_GuardClauses_ThrowArgumentNullException(string nullParam)
    {
        RequestDelegate? next = nullParam == "next" ? null : _ => Task.CompletedTask;
        IShellResolver? resolver = nullParam == "resolver" ? null : new NullShellResolver();
        IShellRegistry? registry = nullParam == "registry" ? null : new FakeRegistry();
        DynamicShellEndpointDataSource? dataSource = nullParam == "endpointDataSource" ? null : new DynamicShellEndpointDataSource();
        ShellMiddlewarePipelineRegistry? pipelineRegistry = nullParam == "pipelineRegistry" ? null : new ShellMiddlewarePipelineRegistry();
        IMemoryCache? cache = nullParam == "cache" ? null : new MemoryCache(new MemoryCacheOptions());
        IOptions<ShellMiddlewareOptions>? options = nullParam == "options" ? null : Options.Create(new ShellMiddlewareOptions());

        var ex = Assert.Throws<ArgumentNullException>(() => new ShellMiddleware(next!, resolver!, registry!, dataSource!, pipelineRegistry!, cache!, options!));
        Assert.Equal(nullParam, ex.ParamName);
    }

    // =================================================================
    // Test doubles
    // =================================================================

    internal static ShellMiddleware CreateMiddleware(
        RequestDelegate next,
        IShellResolver? resolver = null,
        IShellRegistry? registry = null,
        DynamicShellEndpointDataSource? endpointDataSource = null,
        ShellMiddlewarePipelineRegistry? pipelineRegistry = null,
        IMemoryCache? cache = null,
        IOptions<ShellMiddlewareOptions>? options = null) =>
        new(
            next,
            resolver ?? new NullShellResolver(),
            registry ?? new FakeRegistry(),
            endpointDataSource ?? new DynamicShellEndpointDataSource(),
            pipelineRegistry ?? new ShellMiddlewarePipelineRegistry(),
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            options ?? Options.Create(new ShellMiddlewareOptions()));

    private static RouteEndpoint CreateEndpoint(
        string pattern,
        ShellId shellId,
        int generation,
        ShellSettings settings,
        string featureName) =>
        new(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(pattern),
            order: 0,
            new EndpointMetadataCollection(
                new ShellEndpointMetadata(shellId, generation, settings, featureName),
                new EndpointOwnershipMetadata(EndpointOwnerKind.DynamicShell, featureName, shellId, generation),
                new HttpMethodMetadata(["GET"])),
            displayName: $"{pattern} (generation {generation})");

    private static (DefaultHttpContext Context, FireableResponseFeature Response) CreateHttpContextWithFireableResponse(
        IServiceProvider? requestServices = null)
    {
        var ctx = new DefaultHttpContext();
        if (requestServices is not null)
            ctx.RequestServices = requestServices;
        var response = new FireableResponseFeature();
        ctx.Features.Set<IHttpResponseFeature>(response);
        return (ctx, response);
    }

    private interface ITestService;

    private sealed class TestService : ITestService;

    private sealed class NullShellResolver : IShellResolver
    {
        public Task<ShellId?> ResolveAsync(ShellResolutionContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult<ShellId?>(null);
    }

    private sealed class FixedShellResolver(ShellId shellId) : IShellResolver
    {
        public Task<ShellId?> ResolveAsync(ShellResolutionContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult<ShellId?>(shellId);
    }

    internal sealed class FakeRegistry(params FakeShell[] shells) : IShellRegistry
    {
        private readonly FakeShell[] _shells = shells;

        public Task<IShell> GetOrActivateAsync(string name, CancellationToken ct = default)
            => GetActive(name) is { } active
                ? Task.FromResult(active)
                : Task.FromException<IShell>(new ShellBlueprintNotFoundException(name));
        public Task<IShell> ActivateAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ReloadResult> ReloadAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReloadResult>> ReloadActiveAsync(ReloadOptions? options = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IDrainOperation> DrainAsync(IShell shell, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UnregisterBlueprintAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProvidedBlueprint?> GetBlueprintAsync(string name, CancellationToken ct = default) => Task.FromResult<ProvidedBlueprint?>(null);
        public Task<IShellBlueprintManager?> GetManagerAsync(string name, CancellationToken ct = default) => Task.FromResult<IShellBlueprintManager?>(null);
        public Task<ShellPage> ListAsync(ShellListQuery query, CancellationToken ct = default) => Task.FromResult(new ShellPage([], null));
        public IShell? GetActive(string name)
            => _shells
                .Where(shell => string.Equals(shell.Descriptor.Name, name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(shell => shell.Descriptor.Generation)
                .FirstOrDefault();
        public IReadOnlyCollection<IShell> GetAll(string name) =>
            _shells.Where(shell => string.Equals(shell.Descriptor.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        public IReadOnlyCollection<IShell> GetActiveShells() => _shells;
        public void Subscribe(IShellLifecycleSubscriber subscriber) { }
        public void Unsubscribe(IShellLifecycleSubscriber subscriber) { }
    }

    internal sealed class FakeShell(ShellDescriptor descriptor, IServiceProvider provider) : IShell
    {
        private int _activeScopes;

        public ShellDescriptor Descriptor { get; } = descriptor;
        public ShellLifecycleState State => ShellLifecycleState.Active;
        public IServiceProvider ServiceProvider { get; } = provider;
        public IDrainOperation? Drain => null;
        public int ActiveScopeCount => Volatile.Read(ref _activeScopes);

        public IShellScope BeginScope()
        {
            Interlocked.Increment(ref _activeScopes);
            var scope = ServiceProvider.CreateAsyncScope();
            return new FakeScope(this, scope);
        }

        public static FakeShell WithServices(Action<IServiceCollection> configure, string name = "TestShell", int generation = 1)
        {
            var services = new ServiceCollection();
            configure(services);
            return new FakeShell(ShellDescriptor.Create(name, generation), services.BuildServiceProvider());
        }

        private sealed class FakeScope(FakeShell owner, AsyncServiceScope inner) : IShellScope
        {
            private int _disposed;

            public IShell Shell => owner;
            public IServiceProvider ServiceProvider => inner.ServiceProvider;

            public async ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    return;
                try { await inner.DisposeAsync(); }
                finally { Interlocked.Decrement(ref owner._activeScopes); }
            }
        }
    }

    /// <summary>
    /// A custom <see cref="HttpResponseFeature"/> that captures <c>OnCompleted</c> callbacks so
    /// tests can fire them on demand, simulating what the ASP.NET Core server does after the
    /// response is sent. <see cref="DefaultHttpContext"/> has no built-in mechanism to trigger
    /// these callbacks outside of a running server.
    /// </summary>
    internal sealed class FireableResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onCompleted = [];

        public override void OnCompleted(Func<object, Task> callback, object state)
            => _onCompleted.Add((callback, state));

        public async Task FireOnCompletedAsync()
        {
            foreach (var (callback, state) in _onCompleted)
                await callback(state);
        }
    }
}
