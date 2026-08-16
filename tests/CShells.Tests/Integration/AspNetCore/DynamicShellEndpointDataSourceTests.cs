using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using CShells.AspNetCore.Middleware;
using CShells.AspNetCore.Routing;
using CShells.AspNetCore.Notifications;
using CShells.AspNetCore.Features;
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

namespace CShells.Tests.Integration.AspNetCore;

public class DynamicShellEndpointDataSourceTests
{
    [Fact(DisplayName = "RemoveEndpoints by generation preserves endpoints from other generations")]
    public void RemoveByGeneration_PreservesOtherGenerations()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();

        var gen1Endpoint = CreateEndpoint("default/api/hello", shellId, generation: 1, settings);
        var gen2Endpoint = CreateEndpoint("default/api/hello", shellId, generation: 2, settings);

        dataSource.AddEndpoints([gen1Endpoint]);
        dataSource.AddEndpoints([gen2Endpoint]);
        Assert.Equal(2, dataSource.Endpoints.Count);

        // Remove only generation 1 — simulates old shell deactivating after reload.
        dataSource.RemoveEndpoints(shellId, generation: 1);

        Assert.Single(dataSource.Endpoints);
        var remaining = (RouteEndpoint)dataSource.Endpoints[0];
        Assert.Equal(2, remaining.Metadata.GetMetadata<ShellEndpointMetadata>()!.Generation);
    }

    [Fact(DisplayName = "RemoveEndpoints by ShellId removes all generations")]
    public void RemoveByShellId_RemovesAllGenerations()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();

        dataSource.AddEndpoints([CreateEndpoint("default/api/a", shellId, 1, settings)]);
        dataSource.AddEndpoints([CreateEndpoint("default/api/a", shellId, 2, settings)]);
        Assert.Equal(2, dataSource.Endpoints.Count);

        dataSource.RemoveEndpoints(shellId);

        Assert.Empty(dataSource.Endpoints);
    }

    [Fact(DisplayName = "Reload sequence: new generation endpoints survive old generation teardown")]
    public void ReloadSequence_NewEndpointsSurviveOldTeardown()
    {
        // Simulates the exact reload sequence from ShellRegistry.ReloadAsync:
        // 1. New shell (gen 2) transitions Initializing→Active → register gen 2 endpoints
        // 2. Old shell (gen 1) transitions Active→Deactivating → remove gen 1 endpoints only
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();

        // Step 0: Initial activation — gen 1 endpoints registered.
        dataSource.AddEndpoints([CreateEndpoint("default/api/items", shellId, 1, settings)]);

        // Step 1: Reload creates gen 2, which removes-then-adds (as the handler does).
        dataSource.RemoveEndpoints(shellId);
        dataSource.AddEndpoints([CreateEndpoint("default/api/items", shellId, 2, settings)]);

        // Step 2: Old gen 1 deactivates — only removes its own generation.
        dataSource.RemoveEndpoints(shellId, generation: 1);

        // Gen 2 endpoint must survive.
        Assert.Single(dataSource.Endpoints);
        var survivor = (RouteEndpoint)dataSource.Endpoints[0];
        Assert.Equal(2, survivor.Metadata.GetMetadata<ShellEndpointMetadata>()!.Generation);
    }

    [Fact(DisplayName = "No-op endpoint removal does not notify routing")]
    public void RemoveEndpoints_WhenNoEndpointsRemoved_DoesNotNotify()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();
        var changes = 0;

        dataSource.AddEndpoints([CreateEndpoint("default/api/items", shellId, 1, settings)]);

        using var registration = dataSource.GetChangeToken().RegisterChangeCallback(_ => changes++, null);

        dataSource.RemoveEndpoints(shellId, generation: 2);

        Assert.Equal(0, changes);
        Assert.Single(dataSource.Endpoints);
    }

    [Fact(DisplayName = "Endpoint registration handler removes endpoints when drain begins")]
    public async Task Handler_OnDraining_RemovesGenerationEndpoints()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();
        var shell = new FakeShell(ShellDescriptor.Create("default", 1), ShellLifecycleState.Draining);
        var handler = new ShellEndpointRegistrationHandler(
            dataSource,
            new NoopFeatureFactory(),
            new EndpointRouteBuilderAccessor(),
            new ApplicationBuilderAccessor(),
            new ShellMiddlewarePipelineRegistry());

        dataSource.AddEndpoints([CreateEndpoint("default/api/items", shellId, 1, settings)]);

        await handler.OnStateChangedAsync(shell, ShellLifecycleState.Active, ShellLifecycleState.Draining);

        Assert.Empty(dataSource.Endpoints);
    }

    [Fact(DisplayName = "PublishGeneration rejects equivalent templates and preserves the previous snapshot")]
    public void PublishGeneration_EquivalentTemplates_PreservesPreviousSnapshot()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();

        dataSource.PublishGeneration(shellId, 1, [CreateEndpoint("api/items/{id}", shellId, 1, settings, "FeatureV1")]);

        var conflictingShell = new ShellId("other");
        var exception = Assert.Throws<ShellEndpointConflictException>(() =>
            dataSource.PublishGeneration(conflictingShell, 1, [CreateEndpoint("api/items/{name}", conflictingShell, 1, settings, "FeatureV2")]));

        Assert.Contains("DynamicShell:FeatureV2", exception.Message);
        Assert.Contains("DynamicShell:FeatureV1", exception.Message);
        var remaining = Assert.Single(dataSource.Endpoints);
        Assert.Equal("default", remaining.Metadata.GetMetadata<ShellEndpointMetadata>()!.ShellId.Name);
    }

    [Fact(DisplayName = "PublishGeneration rejects overlapping multi-method routes")]
    public void PublishGeneration_OverlappingMethods_RejectsCandidate()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellA = new ShellId("a");
        var shellB = new ShellId("b");
        var settings = new ShellSettings();

        dataSource.PublishGeneration(shellA, 1, [CreateEndpoint("api/items", shellA, 1, settings, "A", ["POST"])]);

        var exception = Assert.Throws<ShellEndpointConflictException>(() =>
            dataSource.PublishGeneration(shellB, 1, [CreateEndpoint("api/items", shellB, 1, settings, "B", ["GET", "POST"])]));

        Assert.Contains("DynamicShell:B", exception.Message);
        Assert.Contains("DynamicShell:A", exception.Message);
        Assert.Single(dataSource.Endpoints);
    }

    [Fact(DisplayName = "PublishGeneration rejects same-batch collisions before publication")]
    public void PublishGeneration_SameBatchCollision_LeavesSnapshotUnchanged()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();

        var exception = Assert.Throws<ShellEndpointConflictException>(() =>
            dataSource.PublishGeneration(shellId, 1,
            [
                CreateEndpoint("api/items/{id}", shellId, 1, settings, "First"),
                CreateEndpoint("api/items/{name}", shellId, 1, settings, "Second"),
            ]));

        Assert.Contains("DynamicShell:First", exception.Message);
        Assert.Contains("DynamicShell:Second", exception.Message);
        Assert.Empty(dataSource.Endpoints);
    }

    [Fact(DisplayName = "PublishGeneration rejects host conflicts with deterministic host owner")]
    public void PublishGeneration_HostConflict_IdentifiesBothOwners()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();
        dataSource.SetHostEndpoints([CreateHostEndpoint("api/items", "Host API", ["GET"])]);

        var exception = Assert.Throws<ShellEndpointConflictException>(() =>
            dataSource.PublishGeneration(shellId, 1, [CreateEndpoint("api/items", shellId, 1, settings, "Feature", ["GET"])]));

        Assert.Contains("DynamicShell:Feature", exception.Message);
        Assert.Contains("Host:Host API", exception.Message);
        Assert.Empty(dataSource.Endpoints);
    }

    [Fact(DisplayName = "PublishGeneration swaps complete snapshots without exposing an empty state")]
    public void PublishGeneration_SuccessfulReplacement_ExposesOnlyCompleteSnapshots()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();
        dataSource.PublishGeneration(shellId, 1, [CreateEndpoint("api/items", shellId, 1, settings, "FeatureV1")]);

        IReadOnlyList<Endpoint>? observed = null;
        using var registration = dataSource.GetChangeToken().RegisterChangeCallback(_ => observed = dataSource.Endpoints, null);
        dataSource.PublishGeneration(shellId, 2,
        [
            CreateEndpoint("api/items", shellId, 2, settings, "FeatureV2"),
            CreateEndpoint("api/status", shellId, 2, settings, "FeatureV2"),
        ]);

        Assert.NotNull(observed);
        Assert.Equal(2, observed!.Count);
        Assert.All(observed, endpoint => Assert.Equal(2, endpoint.Metadata.GetMetadata<ShellEndpointMetadata>()!.Generation));
    }

    [Fact(DisplayName = "Failed feature mapping preserves the previously published generation")]
    public void RegisterActiveShell_MappingFailure_PreservesPreviousGeneration()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings(shellId, ["Broken"]);
        dataSource.PublishGeneration(shellId, 1, [CreateEndpoint("api/items", shellId, 1, settings, "FeatureV1")]);

        var services = new ServiceCollection();
        services.AddSingleton(settings);
        services.AddSingleton<IEnumerable<ShellFeatureDescriptor>>(
            [new ShellFeatureDescriptor("Broken") { StartupType = typeof(ThrowingWebFeature) }]);
        var shell = new TestShell(ShellDescriptor.Create("default", 2), services.BuildServiceProvider());
        var handler = new ShellEndpointRegistrationHandler(
            dataSource,
            new ThrowingFeatureFactory(),
            new EndpointRouteBuilderAccessor { EndpointRouteBuilder = new TestEndpointRouteBuilder(shell.ServiceProvider) },
            new ApplicationBuilderAccessor { ApplicationBuilder = new ApplicationBuilder(shell.ServiceProvider) },
            new ShellMiddlewarePipelineRegistry());

        Assert.Throws<InvalidOperationException>(() => handler.RegisterActiveShell(shell));
        var remaining = Assert.Single(dataSource.Endpoints);
        Assert.Equal(1, remaining.Metadata.GetMetadata<ShellEndpointMetadata>()!.Generation);
    }

    [Fact(DisplayName = "Repeated collectible feature generations unload after replacement and removal")]
    public async Task CollectibleFeatureGenerations_UnloadAfterReplacementAndRemoval()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("collectible");
        var fixtureAssemblyPath = Path.Combine(AppContext.BaseDirectory, "CShells.DynamicFeatureFixture.dll");

        Assert.True(File.Exists(fixtureAssemblyPath), $"Dynamic feature fixture was not copied to '{fixtureAssemblyPath}'.");

        for (var cycle = 1; cycle <= 5; cycle++)
        {
            var loadContext = MapReplaceAndRemoveCollectibleGeneration(
                dataSource,
                shellId,
                generation: cycle * 2 - 1,
                fixtureAssemblyPath);

            Assert.Empty(dataSource.Endpoints);
            await AssertCollectibleAsync(loadContext, cycle);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference MapReplaceAndRemoveCollectibleGeneration(
        DynamicShellEndpointDataSource dataSource,
        ShellId shellId,
        int generation,
        string fixtureAssemblyPath)
    {
        var loadContext = new CollectibleFeatureLoadContext();
        var loadContextWeakReference = new WeakReference(loadContext);
        var fixtureAssembly = loadContext.LoadFromAssemblyPath(fixtureAssemblyPath);
        var featureType = fixtureAssembly.GetType(
            "CShells.DynamicFeatureFixture.DynamicFeature",
            throwOnError: true)!;

        Assert.Same(loadContext, AssemblyLoadContext.GetLoadContext(featureType.Assembly));

        var feature = (IWebShellFeature)Activator.CreateInstance(featureType)!;
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var innerBuilder = new TestEndpointRouteBuilder(serviceProvider);
        var shellSettings = new ShellSettings(shellId);
        var shellBuilder = new ShellEndpointRouteBuilder(
            innerBuilder,
            shellId,
            generation,
            shellSettings,
            serviceProvider,
            pathPrefix: null);

        // Use the same route-builder seam used by ShellEndpointRegistrationHandler: feature code
        // maps into ShellEndpointRouteBuilder, which materializes the complete candidate before
        // DynamicShellEndpointDataSource publishes it.
        feature.MapEndpoints(shellBuilder, environment: null);
        var collectibleCandidate = shellBuilder.GetEndpoints().ToArray();
        Assert.Single(collectibleCandidate);
        dataSource.PublishGeneration(shellId, generation, collectibleCandidate);

        // Replacing the candidate retires its endpoint while publishing the next complete
        // snapshot. Drive the replacement through the lifecycle handler's Draining transition
        // so the test exercises the same removal seam used by a real shell drain.
        var replacementSettings = new ShellSettings(shellId);
        dataSource.PublishGeneration(
            shellId,
            generation + 1,
            [CreateEndpoint("/replacement", shellId, generation + 1, replacementSettings, "Replacement")]);

        var handler = new ShellEndpointRegistrationHandler(
            dataSource,
            new NoopFeatureFactory(),
            new EndpointRouteBuilderAccessor(),
            new ApplicationBuilderAccessor(),
            new ShellMiddlewarePipelineRegistry());
        var replacementShell = new FakeShell(
            ShellDescriptor.Create(shellId.Name, generation + 1),
            ShellLifecycleState.Draining);
        handler.OnStateChangedAsync(replacementShell, ShellLifecycleState.Deactivating, ShellLifecycleState.Draining)
            .GetAwaiter()
            .GetResult();

        loadContext.Unload();
        return loadContextWeakReference;
    }

    private static async Task AssertCollectibleAsync(WeakReference loadContext, int cycle)
    {
        for (var attempt = 0; attempt < 40 && loadContext.IsAlive; attempt++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            await Task.Yield();
        }

        Assert.False(loadContext.IsAlive,
            $"Collectible feature load context remained rooted after cycle {cycle}; " +
            "published or retired endpoint state still retains feature assembly code.");
    }

    private sealed class CollectibleFeatureLoadContext : AssemblyLoadContext
    {
        public CollectibleFeatureLoadContext()
            : base($"CShells.DynamicFeatureFixture-{Guid.NewGuid():N}", isCollectible: true)
        {
        }

        protected override Assembly? Load(AssemblyName assemblyName) =>
            AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
    }

    private static RouteEndpoint CreateEndpoint(
        string pattern,
        ShellId shellId,
        int generation,
        ShellSettings settings,
        string featureName = "Feature",
        IReadOnlyList<string>? methods = null)
    {
        return new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(pattern),
            order: 0,
            new EndpointMetadataCollection(
                new ShellEndpointMetadata(shellId, generation, settings, featureName),
                new EndpointOwnershipMetadata(EndpointOwnerKind.DynamicShell, featureName, shellId, generation),
                new HttpMethodMetadata(methods ?? ["GET"])),
            displayName: $"{pattern} (gen {generation})");
    }

    private static RouteEndpoint CreateHostEndpoint(string pattern, string displayName, IReadOnlyList<string> methods) =>
        new(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(pattern),
            order: 0,
            new EndpointMetadataCollection(new HttpMethodMetadata(methods)),
            displayName);

    private sealed class FakeShell(ShellDescriptor descriptor, ShellLifecycleState state) : IShell
    {
        public ShellDescriptor Descriptor { get; } = descriptor;

        public ShellLifecycleState State { get; } = state;

        public IServiceProvider ServiceProvider => throw new NotSupportedException();

        public IDrainOperation? Drain => null;

        public IShellScope BeginScope() => throw new NotSupportedException();
    }

    private sealed class NoopFeatureFactory : IShellFeatureFactory
    {
        public T CreateFeature<T>(Type featureType, ShellSettings? shellSettings = null, ShellFeatureContext? featureContext = null)
            where T : class =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingFeatureFactory : IShellFeatureFactory
    {
        public T CreateFeature<T>(Type featureType, ShellSettings? shellSettings = null, ShellFeatureContext? featureContext = null)
            where T : class =>
            (T)(object)new ThrowingWebFeature();
    }

    private sealed class ThrowingWebFeature : IWebShellFeature
    {
        public void ConfigureServices(IServiceCollection services) { }

        public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) =>
            throw new InvalidOperationException("mapping failed");
    }

    private sealed class TestShell(ShellDescriptor descriptor, IServiceProvider provider) : IShell
    {
        public ShellDescriptor Descriptor { get; } = descriptor;
        public ShellLifecycleState State => ShellLifecycleState.Active;
        public IServiceProvider ServiceProvider { get; } = provider;
        public IDrainOperation? Drain => null;
        public IShellScope BeginScope() => throw new NotSupportedException();
    }

    private sealed class TestEndpointRouteBuilder(IServiceProvider provider) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = provider;
        public ICollection<EndpointDataSource> DataSources { get; } = [];
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }
}
