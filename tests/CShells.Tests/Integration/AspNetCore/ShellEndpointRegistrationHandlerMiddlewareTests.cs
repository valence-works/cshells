using CShells.AspNetCore.Features;
using CShells.AspNetCore.Middleware;
using CShells.AspNetCore.Notifications;
using CShells.AspNetCore.Routing;
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for the middleware side of <see cref="ShellEndpointRegistrationHandler"/>: building
/// per-shell pipelines from <see cref="IMiddlewareShellFeature"/>s on activation, ordering,
/// path-prefix semantics, composition failure policy, and generation-aware teardown.
/// </summary>
public class ShellEndpointRegistrationHandlerMiddlewareTests
{
    private readonly ShellMiddlewarePipelineRegistry _pipelines = new();
    private readonly ApplicationBuilderAccessor _appBuilderAccessor;
    private readonly ShellEndpointRegistrationHandler _handler;

    public ShellEndpointRegistrationHandlerMiddlewareTests()
    {
        var rootProvider = new ServiceCollection().BuildServiceProvider();
        _appBuilderAccessor = new ApplicationBuilderAccessor { ApplicationBuilder = new ApplicationBuilder(rootProvider) };
        _handler = new ShellEndpointRegistrationHandler(
            new DynamicShellEndpointDataSource(),
            new ActivatorFeatureFactory(),
            new EndpointRouteBuilderAccessor { EndpointRouteBuilder = new FakeEndpointRouteBuilder(rootProvider) },
            _appBuilderAccessor,
            _pipelines);
    }

    [Fact(DisplayName = "Initializing → Active with middleware features registers a pipeline for the shell generation")]
    public async Task ActiveTransition_WithMiddlewareFeatures_RegistersPipeline()
    {
        var shell = CreateShell("acme", generation: 1, pathPrefix: null, ("Alpha", typeof(AlphaFeature)));

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Initializing, ShellLifecycleState.Active);

        Assert.NotNull(GetPipeline("acme", 1));
    }

    [Fact(DisplayName = "Initializing → Active without middleware features registers no pipeline")]
    public async Task ActiveTransition_WithoutMiddlewareFeatures_RegistersNoPipeline()
    {
        var shell = CreateShell("acme", generation: 1, pathPrefix: null);

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Initializing, ShellLifecycleState.Active);

        Assert.Null(GetPipeline("acme", 1));
    }

    [Fact(DisplayName = "Features apply in ascending Order; a feature without an override defaults to 0")]
    public async Task Pipeline_AppliesFeatures_InAscendingOrder()
    {
        // Discovery order deliberately scrambled relative to Order values.
        var shell = CreateShell("acme", generation: 1, pathPrefix: null,
            ("Late", typeof(LateFeature)),        // Order 10
            ("Default", typeof(DefaultOrderFeature)), // no override → 0
            ("Early", typeof(EarlyFeature)));     // Order -10

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Initializing, ShellLifecycleState.Active);
        var run = await InvokePipelineAsync("acme", 1);

        Assert.Equal(["early", "default", "late"], run.Markers);
        Assert.True(run.ContinuationCalled);
    }

    [Fact(DisplayName = "Features with equal Order preserve feature-discovery order")]
    public async Task Pipeline_EqualOrder_PreservesDiscoveryOrder()
    {
        var shell = CreateShell("acme", generation: 1, pathPrefix: null,
            ("Alpha", typeof(AlphaFeature)),
            ("Bravo", typeof(BravoFeature)));

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Initializing, ShellLifecycleState.Active);
        var run = await InvokePipelineAsync("acme", 1);

        Assert.Equal(["alpha", "bravo"], run.Markers);
    }

    [Fact(DisplayName = "Pipeline is removed on Disposed, but kept through Deactivating and Draining")]
    public async Task TeardownTransitions_RemovePipelineOnlyOnDisposed()
    {
        var shell = CreateShell("acme", generation: 1, pathPrefix: null, ("Alpha", typeof(AlphaFeature)));
        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Initializing, ShellLifecycleState.Active);

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Active, ShellLifecycleState.Deactivating);
        Assert.NotNull(GetPipeline("acme", 1));

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Deactivating, ShellLifecycleState.Draining);
        Assert.NotNull(GetPipeline("acme", 1));

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Draining, ShellLifecycleState.Disposed);
        Assert.Null(GetPipeline("acme", 1));
    }

    [Fact(DisplayName = "Reload: disposing the old generation leaves the new generation's pipeline intact")]
    public async Task Reload_OldGenerationDisposed_NewGenerationKept()
    {
        var gen1 = CreateShell("acme", generation: 1, pathPrefix: null, ("Alpha", typeof(AlphaFeature)));
        var gen2 = CreateShell("acme", generation: 2, pathPrefix: null, ("Alpha", typeof(AlphaFeature)));

        await _handler.OnStateChangedAsync(gen1, ShellLifecycleState.Initializing, ShellLifecycleState.Active);
        // ReloadAsync activates the new generation before deactivating the old one.
        await _handler.OnStateChangedAsync(gen2, ShellLifecycleState.Initializing, ShellLifecycleState.Active);
        await _handler.OnStateChangedAsync(gen1, ShellLifecycleState.Draining, ShellLifecycleState.Disposed);

        Assert.Null(GetPipeline("acme", 1));
        Assert.NotNull(GetPipeline("acme", 2));
    }

    [Fact(DisplayName = "Prefixed shell: features see the stripped PathBase; the continuation sees the full path again")]
    public async Task PrefixedShell_MatchingRequest_StripsAndReappliesPrefix()
    {
        var shell = CreateShell("acme", generation: 1, pathPrefix: "/acme", ("PathCapture", typeof(PathCaptureFeature)));

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Initializing, ShellLifecycleState.Active);
        var run = await InvokePipelineAsync("acme", 1, path: "/acme/orders");

        Assert.Equal("/orders", run.FeaturePath);
        Assert.Equal("/acme", run.FeaturePathBase);
        Assert.True(run.ContinuationCalled);
        Assert.Equal("/acme/orders", run.ContinuationPath);
        Assert.Equal("", run.ContinuationPathBase);
    }

    [Fact(DisplayName = "Prefixed shell: a Path rewrite made by feature middleware survives into the continuation")]
    public async Task PrefixedShell_FeatureRewrite_IsPreservedDownstream()
    {
        var shell = CreateShell("acme", generation: 1, pathPrefix: "/acme", ("Rewrite", typeof(PathRewriteFeature)));

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Initializing, ShellLifecycleState.Active);
        var run = await InvokePipelineAsync("acme", 1, path: "/acme/orders");

        // The feature rewrote "/orders" → "/rewritten/orders"; the terminal re-applies the
        // stripped prefix around the rewrite instead of resetting to the original path.
        Assert.True(run.ContinuationCalled);
        Assert.Equal("/acme/rewritten/orders", run.ContinuationPath);
    }

    [Fact(DisplayName = "Non-prefixed shell: a Path rewrite made by feature middleware flows downstream untouched")]
    public async Task NonPrefixedShell_FeatureRewrite_IsPreservedDownstream()
    {
        var shell = CreateShell("acme", generation: 1, pathPrefix: null, ("Rewrite", typeof(PathRewriteFeature)));

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Initializing, ShellLifecycleState.Active);
        var run = await InvokePipelineAsync("acme", 1, path: "/orders");

        Assert.True(run.ContinuationCalled);
        Assert.Equal("/rewritten/orders", run.ContinuationPath);
    }

    [Fact(DisplayName = "Prefixed shell: a request outside the prefix skips the features but still rejoins the pipeline")]
    public async Task PrefixedShell_NonMatchingRequest_SkipsFeaturesAndContinues()
    {
        var shell = CreateShell("acme", generation: 1, pathPrefix: "/acme", ("PathCapture", typeof(PathCaptureFeature)));

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Initializing, ShellLifecycleState.Active);
        var run = await InvokePipelineAsync("acme", 1, path: "/other");

        Assert.Null(run.FeaturePath);
        Assert.True(run.ContinuationCalled);
        Assert.Equal("/other", run.ContinuationPath);
    }

    [Fact(DisplayName = "A root path prefix (\"/\") means no prefix scoping and does not throw")]
    public async Task RootPathPrefix_TreatedAsNoPrefix()
    {
        var shell = CreateShell("acme", generation: 1, pathPrefix: "/", ("Alpha", typeof(AlphaFeature)));

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Initializing, ShellLifecycleState.Active);
        var run = await InvokePipelineAsync("acme", 1, path: "/anything");

        Assert.Equal(["alpha"], run.Markers);
        Assert.True(run.ContinuationCalled);
    }

    [Fact(DisplayName = "A middleware feature that fails to compose registers a fail-closed 503 pipeline instead of going dark")]
    public async Task CompositionFailure_RegistersFailClosed503Pipeline()
    {
        var shell = CreateShell("acme", generation: 1, pathPrefix: null, ("Broken", typeof(ThrowingFeature)));

        // Must not throw: the lifecycle fan-out would swallow it and leave the shell dark.
        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Initializing, ShellLifecycleState.Active);

        // Bind the tracking continuation on the FIRST Get — binding is first-wins.
        var continuationCalled = false;
        var pipeline = _pipelines.Get(new ShellId("acme"), 1, _ => { continuationCalled = true; return Task.CompletedTask; });
        Assert.NotNull(pipeline);

        var ctx = new DefaultHttpContext();
        await pipeline!(ctx);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, ctx.Response.StatusCode);
        Assert.False(continuationCalled);
    }

    [Fact(DisplayName = "Without a captured IApplicationBuilder no pipeline is registered and no exception is thrown")]
    public async Task NoApplicationBuilder_NoPipelineNoThrow()
    {
        _appBuilderAccessor.ApplicationBuilder = null;
        var shell = CreateShell("acme", generation: 1, pathPrefix: null, ("Alpha", typeof(AlphaFeature)));

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Initializing, ShellLifecycleState.Active);

        Assert.Null(GetPipeline("acme", 1));
    }

    [Fact(DisplayName = "RegisterActiveShell replays registration for a shell that activated before MapShells")]
    public async Task RegisterActiveShell_ReplaysRegistration_AfterBuilderCaptured()
    {
        var savedBuilder = _appBuilderAccessor.ApplicationBuilder;
        _appBuilderAccessor.ApplicationBuilder = null;
        var shell = CreateShell("acme", generation: 1, pathPrefix: null, ("Alpha", typeof(AlphaFeature)));

        await _handler.OnStateChangedAsync(shell, ShellLifecycleState.Initializing, ShellLifecycleState.Active);
        Assert.Null(GetPipeline("acme", 1));

        // MapShells captures the builder and replays registration for active shells.
        _appBuilderAccessor.ApplicationBuilder = savedBuilder;
        _handler.RegisterActiveShell(shell);

        Assert.NotNull(GetPipeline("acme", 1));
    }

    // =================================================================
    // Helpers + test doubles
    // =================================================================

    private RequestDelegate? GetPipeline(string name, int generation) =>
        _pipelines.Get(new ShellId(name), generation, _ => Task.CompletedTask);

    private static IShell CreateShell(string name, int generation, string? pathPrefix, params (string Id, Type Type)[] features)
    {
        var settings = new ShellSettings(new ShellId(name), features.Select(f => f.Id).ToList());
        if (pathPrefix is not null)
            settings.ConfigurationData["WebRouting:Path"] = pathPrefix;

        var descriptors = features
            .Select(f => new ShellFeatureDescriptor(f.Id) { StartupType = f.Type })
            .ToList();

        var services = new ServiceCollection();
        services.AddSingleton(settings);
        services.AddSingleton<IEnumerable<ShellFeatureDescriptor>>(descriptors);
        return new ShellMiddlewareTests.FakeShell(ShellDescriptor.Create(name, generation), services.BuildServiceProvider());
    }

    private sealed record PipelineRun(
        List<string> Markers,
        bool ContinuationCalled,
        string? ContinuationPath,
        string? ContinuationPathBase,
        string? FeaturePath,
        string? FeaturePathBase);

    private async Task<PipelineRun> InvokePipelineAsync(string shellName, int generation, string path = "/")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Items["markers"] = new List<string>();

        RequestDelegate continuation = c =>
        {
            c.Items["continued"] = true;
            c.Items["cont-path"] = c.Request.Path.Value;
            c.Items["cont-pathbase"] = c.Request.PathBase.Value;
            return Task.CompletedTask;
        };
        var pipeline = _pipelines.Get(new ShellId(shellName), generation, continuation)
            ?? throw new InvalidOperationException($"No pipeline registered for '{shellName}' generation {generation}.");

        await pipeline(ctx);

        return new PipelineRun(
            (List<string>)ctx.Items["markers"]!,
            ctx.Items.ContainsKey("continued"),
            ctx.Items.TryGetValue("cont-path", out var cp) ? (string?)cp : null,
            ctx.Items.TryGetValue("cont-pathbase", out var cpb) ? (string?)cpb : null,
            ctx.Items.TryGetValue("feature-path", out var fp) ? (string?)fp : null,
            ctx.Items.TryGetValue("feature-pathbase", out var fpb) ? (string?)fpb : null);
    }

    private abstract class MarkerFeature(string marker, int order) : IMiddlewareShellFeature
    {
        public int Order => order;
        public void ConfigureServices(IServiceCollection services) { }

        public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment) => AddMarker(app, marker);

        public static void AddMarker(IApplicationBuilder app, string marker) =>
            app.Use(next => ctx =>
            {
                ((List<string>)ctx.Items["markers"]!).Add(marker);
                return next(ctx);
            });
    }

    private sealed class EarlyFeature() : MarkerFeature("early", -10);

    private sealed class LateFeature() : MarkerFeature("late", 10);

    private sealed class AlphaFeature() : MarkerFeature("alpha", 0);

    private sealed class BravoFeature() : MarkerFeature("bravo", 0);

    /// <summary>Does not override <see cref="IMiddlewareShellFeature.Order"/> — exercises the interface default of 0.</summary>
    private sealed class DefaultOrderFeature : IMiddlewareShellFeature
    {
        public void ConfigureServices(IServiceCollection services) { }
        public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment) => MarkerFeature.AddMarker(app, "default");
    }

    private sealed class PathCaptureFeature : IMiddlewareShellFeature
    {
        public void ConfigureServices(IServiceCollection services) { }

        public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment) =>
            app.Use(next => ctx =>
            {
                ctx.Items["feature-path"] = ctx.Request.Path.Value;
                ctx.Items["feature-pathbase"] = ctx.Request.PathBase.Value;
                return next(ctx);
            });
    }

    /// <summary>Rewrites the request path before calling next — the rewrite must survive into the continuation.</summary>
    private sealed class PathRewriteFeature : IMiddlewareShellFeature
    {
        public void ConfigureServices(IServiceCollection services) { }

        public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment) =>
            app.Use(next => ctx =>
            {
                ctx.Request.Path = new PathString("/rewritten" + ctx.Request.Path.Value);
                return next(ctx);
            });
    }

    private sealed class ThrowingFeature : IMiddlewareShellFeature
    {
        public void ConfigureServices(IServiceCollection services) { }
        public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment) =>
            throw new InvalidOperationException("broken feature");
    }

    private sealed class ActivatorFeatureFactory : IShellFeatureFactory
    {
        public T CreateFeature<T>(Type featureType, ShellSettings? shellSettings = null, ShellFeatureContext? featureContext = null)
            where T : class =>
            (T)Activator.CreateInstance(featureType)!;
    }

    private sealed class FakeEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = [];
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }
}
