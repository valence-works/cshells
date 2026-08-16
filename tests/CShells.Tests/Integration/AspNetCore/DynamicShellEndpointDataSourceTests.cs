using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using CShells.AspNetCore.Extensions;
using CShells.AspNetCore.Middleware;
using CShells.AspNetCore.Routing;
using CShells.AspNetCore.Notifications;
using CShells.DependencyInjection;
using CShells.AspNetCore.Features;
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
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

    [Fact(DisplayName = "A previously returned change token remains safe to register after publication")]
    public void GetChangeToken_ConcurrentPublication_DoesNotDisposeReturnedToken()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();
        var token = dataSource.GetChangeToken();

        dataSource.PublishGeneration(shellId, 1, [CreateEndpoint("api/items", shellId, 1, settings)]);

        using var registration = token.RegisterChangeCallback(_ => { }, null);
        Assert.True(token.HasChanged);
    }

    [Fact(DisplayName = "Change-token reads and registrations remain safe during repeated publication")]
    public async Task GetChangeToken_ParallelPublication_DoesNotRaceTokenDisposal()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("tokens");
        var settings = new ShellSettings(shellId);
        var errors = new ConcurrentQueue<Exception>();

        var writer = Task.Run(() =>
        {
            for (var generation = 1; generation <= 2_000; generation++)
            {
                dataSource.PublishGeneration(
                    shellId,
                    generation,
                    [CreateEndpoint("api/token", shellId, generation, settings)]);
            }
        });
        var readers = Enumerable.Range(0, 4).Select(readerIndex => Task.Run(() =>
        {
            for (var attempt = 0; attempt < 2_000; attempt++)
            {
                try
                {
                    var token = dataSource.GetChangeToken();
                    using var registration = token.RegisterChangeCallback(_ => { }, null);
                    _ = token.HasChanged;
                    _ = dataSource.Endpoints.Count;
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }
        }));

        await Task.WhenAll(readers.Append(writer));

        Assert.Empty(errors);
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
        Assert.Contains("shell 'other' generation 1", exception.Message);
        Assert.Contains("shell 'default' generation 1", exception.Message);
        Assert.Equal(conflictingShell, exception.Conflict.CandidateOwner.ShellId);
        Assert.Equal(1, exception.Conflict.CandidateOwner.Generation);
        Assert.Equal("FeatureV2", exception.Conflict.CandidateOwner.OwnerId);
        Assert.Equal(shellId, exception.Conflict.ExistingOwner.ShellId);
        Assert.Equal(1, exception.Conflict.ExistingOwner.Generation);
        Assert.Equal("FeatureV1", exception.Conflict.ExistingOwner.OwnerId);
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

    [Fact(DisplayName = "Method overlap compares HTTP methods case-insensitively")]
    public void MethodsConflict_MethodCaseDiffers_ReturnsTrue()
    {
        // HttpMethodMetadata normalizes methods to upper case, so exercise the data source's
        // comparison seam directly to keep its behavior correct for any non-normalized inventory.
        Assert.True(DynamicShellEndpointDataSource.MethodsConflict(["GET"], ["get"]));
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
        Assert.Equal(shellId, exception.Conflict.CandidateOwner.ShellId);
        Assert.Equal(1, exception.Conflict.CandidateOwner.Generation);
        Assert.Equal("Feature", exception.Conflict.CandidateOwner.OwnerId);
        Assert.Equal(EndpointOwnerKind.Host, exception.Conflict.ExistingOwner.OwnerKind);
        Assert.Equal("Host API", exception.Conflict.ExistingOwner.OwnerId);
        Assert.Null(exception.Conflict.ExistingOwner.ShellId);
        Assert.Null(exception.Conflict.ExistingOwner.Generation);
        Assert.Empty(dataSource.Endpoints);
    }

    [Fact(DisplayName = "PublishGeneration swaps complete snapshots without exposing an empty state")]
    public void PublishGeneration_SuccessfulReplacement_ExposesOnlyCompleteSnapshots()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();
        dataSource.PublishGeneration(shellId, 1, [CreateEndpoint("api/items", shellId, 1, settings, "FeatureV1")]);

        var observed = (IReadOnlyList<Endpoint>?)null;
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

    [Fact(DisplayName = "Overlapping prepared publications are linearized and rollback cannot restore an intermediate generation")]
    public void PrepareGeneration_OverlappingCommitAndRollback_DoesNotResurrectRoutes()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("linear");
        var settings = new ShellSettings(shellId);
        dataSource.PublishGeneration(shellId, 1, [CreateEndpoint("api/one", shellId, 1, settings)]);
        using var generationTwo = dataSource.PrepareGeneration(
            shellId,
            2,
            [CreateEndpoint("api/two", shellId, 2, settings)]);
        using var rejectedGenerationThree = dataSource.PrepareGeneration(
            shellId,
            3,
            [CreateEndpoint("api/three", shellId, 3, settings)]);

        generationTwo.Commit();
        Assert.Throws<InvalidOperationException>(() => rejectedGenerationThree.Commit());
        generationTwo.Rollback();

        rejectedGenerationThree.Commit();
        rejectedGenerationThree.Rollback();

        AssertGeneration(dataSource, expectedGeneration: 1);
        Assert.Equal("api/one", ((RouteEndpoint)dataSource.Endpoints.Single()).RoutePattern.RawText);
    }

    [Fact(DisplayName = "Completing a committed publication allows the next prepared generation to commit")]
    public void PrepareGeneration_Complete_AllowsNextCommit()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("complete");
        var settings = new ShellSettings(shellId);
        dataSource.PublishGeneration(shellId, 1, [CreateEndpoint("api/one", shellId, 1, settings)]);
        using var generationTwo = dataSource.PrepareGeneration(
            shellId,
            2,
            [CreateEndpoint("api/two", shellId, 2, settings)]);
        using var generationThree = dataSource.PrepareGeneration(
            shellId,
            3,
            [CreateEndpoint("api/three", shellId, 3, settings)]);

        generationTwo.Commit();
        dataSource.RemoveEndpoints(shellId, 2);
        AssertGeneration(dataSource, expectedGeneration: 2);
        generationTwo.Complete();
        Assert.Empty(dataSource.Endpoints);
        generationThree.Commit();
        generationThree.Complete();

        AssertGeneration(dataSource, expectedGeneration: 3);
    }

    [Fact(DisplayName = "Same-shell mutations are rejected while a committed publication retains rollback evidence")]
    public void PrepareGeneration_PendingCommit_GuardsSameShellMutations()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("guarded");
        var settings = new ShellSettings(shellId);
        dataSource.PublishGeneration(shellId, 1, [CreateEndpoint("api/one", shellId, 1, settings)]);
        using var generationTwo = dataSource.PrepareGeneration(
            shellId,
            2,
            [CreateEndpoint("api/two", shellId, 2, settings)]);
        generationTwo.Commit();

        Assert.Throws<InvalidOperationException>(() => dataSource.AddEndpoints(
            [CreateEndpoint("api/add", shellId, 2, settings)]));
        Assert.Throws<InvalidOperationException>(() => dataSource.AddEndpoints(
            [CreateHostEndpoint("api/one", "conflicting host endpoint", ["GET"])]));
        Assert.Throws<InvalidOperationException>(() => dataSource.SetHostEndpoints(
            [CreateHostEndpoint("api/one", "conflicting host inventory", ["GET"])]));
        Assert.Throws<InvalidOperationException>(() => dataSource.RemoveEndpoints(shellId));
        dataSource.RemoveEndpoints(shellId, 2);
        dataSource.RetireGeneration(shellId, 2);
        AssertGeneration(dataSource, expectedGeneration: 2);
        Assert.Throws<InvalidOperationException>(dataSource.Clear);

        generationTwo.Rollback();
        AssertGeneration(dataSource, expectedGeneration: 1);
        dataSource.RemoveEndpoints(shellId);
        Assert.Empty(dataSource.Endpoints);
    }

    [Fact(DisplayName = "A pending commit blocks cross-shell publication until rollback evidence is released")]
    public void PrepareGeneration_PendingCommit_GuardsCrossShellPublication()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellA = new ShellId("a");
        var shellB = new ShellId("b");
        var settingsA = new ShellSettings(shellA);
        var settingsB = new ShellSettings(shellB);
        dataSource.PublishGeneration(shellA, 1, [CreateEndpoint("api/retired", shellA, 1, settingsA)]);
        using var publicationA = dataSource.PrepareGeneration(
            shellA,
            2,
            [CreateEndpoint("api/current", shellA, 2, settingsA)]);
        publicationA.Commit();
        using var publicationB = dataSource.PrepareGeneration(
            shellB,
            1,
            [CreateEndpoint("api/retired", shellB, 1, settingsB)]);

        Assert.Throws<InvalidOperationException>(() => publicationB.Commit());

        publicationA.Rollback();
        Assert.Throws<ShellEndpointConflictException>(() => publicationB.Commit());
        Assert.Equal(shellA, dataSource.Endpoints.Single().Metadata.GetMetadata<ShellEndpointMetadata>()!.ShellId);
    }

    [Fact(DisplayName = "Prepared publications revalidate conflicts at commit")]
    public void PrepareGeneration_ConcurrentConflict_SecondCommitIsRejected()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var settings = new ShellSettings();
        var shellA = new ShellId("a");
        var shellB = new ShellId("b");
        using var publicationA = dataSource.PrepareGeneration(
            shellA,
            1,
            [CreateEndpoint("api/shared", shellA, 1, settings, "FeatureA")]);
        using var publicationB = dataSource.PrepareGeneration(
            shellB,
            1,
            [CreateEndpoint("api/shared", shellB, 1, settings, "FeatureB")]);

        publicationA.Commit();
        publicationA.Complete();
        var exception = Assert.Throws<ShellEndpointConflictException>(() => publicationB.Commit());

        Assert.Equal("FeatureB", exception.Conflict.CandidateOwner.OwnerId);
        Assert.Equal("FeatureA", exception.Conflict.ExistingOwner.OwnerId);
        Assert.Equal(shellA, dataSource.Endpoints.Single().Metadata.GetMetadata<ShellEndpointMetadata>()!.ShellId);
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

        var exception = Assert.Throws<ShellGenerationActivationException>(() => handler.RegisterActiveShell(shell));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        var remaining = Assert.Single(dataSource.Endpoints);
        Assert.Equal(1, remaining.Metadata.GetMetadata<ShellEndpointMetadata>()!.Generation);
    }

    [Fact(DisplayName = "Rejected reload restores the prior endpoint snapshot and keeps its pipeline live")]
    public async Task Reload_LaterSubscriberRejectsCandidate_RestoresPriorGeneration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShellsAspNetCore(cshells => cshells
            .WithAssemblyContaining<RollbackStableFeature>()
            .AddShell("rollback", shell => shell.WithFeature<RollbackStableFeature>()));
        services.AddSingleton<IShellLifecycleSubscriber, RejectSecondGenerationSubscriber>();

        await using var host = services.BuildServiceProvider();
        CaptureRoutingInfrastructure(host);
        var registry = host.GetRequiredService<IShellRegistry>();
        var dataSource = host.GetRequiredService<DynamicShellEndpointDataSource>();
        var pipelines = host.GetRequiredService<ShellMiddlewarePipelineRegistry>();
        var shellId = new ShellId("rollback");
        var generationOne = await registry.ActivateAsync(shellId.Name);

        AssertGeneration(dataSource, expectedGeneration: 1);
        await AssertPipelineMarkerAsync(pipelines, shellId, generation: 1, "stable");

        var reload = await registry.ReloadAsync(shellId.Name);

        Assert.IsType<ShellGenerationActivationException>(reload.Error);
        Assert.Null(reload.NewShell);
        Assert.Same(generationOne, registry.GetActive(shellId.Name));
        AssertGeneration(dataSource, expectedGeneration: 1);
        await AssertPipelineMarkerAsync(pipelines, shellId, generation: 1, "stable");
        Assert.Null(pipelines.Get(shellId, generation: 2, _ => Task.CompletedTask));
    }

    [Fact(DisplayName = "Middleware composition failure rejects a reload and preserves the prior generation")]
    public async Task Reload_MiddlewareCompositionFails_PreservesPriorGeneration()
    {
        var attempts = new MiddlewareCompositionAttempts();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(attempts);
        services.AddCShellsAspNetCore(cshells => cshells
            .WithAssemblyContaining<FailOnSecondCompositionFeature>()
            .AddShell("composition", shell => shell.WithFeature<FailOnSecondCompositionFeature>()));

        await using var host = services.BuildServiceProvider();
        CaptureRoutingInfrastructure(host);
        var registry = host.GetRequiredService<IShellRegistry>();
        var dataSource = host.GetRequiredService<DynamicShellEndpointDataSource>();
        var pipelines = host.GetRequiredService<ShellMiddlewarePipelineRegistry>();
        var shellId = new ShellId("composition");
        var generationOne = await registry.ActivateAsync(shellId.Name);

        AssertGeneration(dataSource, expectedGeneration: 1);
        await AssertPipelineMarkerAsync(pipelines, shellId, generation: 1, "composition");

        var reload = await registry.ReloadAsync(shellId.Name);

        Assert.IsType<ShellGenerationActivationException>(reload.Error);
        Assert.Null(reload.NewShell);
        Assert.Same(generationOne, registry.GetActive(shellId.Name));
        AssertGeneration(dataSource, expectedGeneration: 1);
        await AssertPipelineMarkerAsync(pipelines, shellId, generation: 1, "composition");
        Assert.Null(pipelines.Get(shellId, generation: 2, _ => Task.CompletedTask));
    }

    [Fact(DisplayName = "A provisional reload is not routing-visible while a later subscriber is still deciding")]
    public async Task Reload_SlowLaterSubscriber_KeepsPriorGenerationRoutableUntilCommit()
    {
        var slowSubscriber = new SlowSecondGenerationSubscriber();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShellsAspNetCore(cshells => cshells
            .WithAssemblyContaining<RollbackStableFeature>()
            .AddShell("provisional", shell => shell.WithFeature<RollbackStableFeature>()));
        services.AddSingleton<IShellLifecycleSubscriber>(slowSubscriber);

        await using var host = services.BuildServiceProvider();
        CaptureRoutingInfrastructure(host);
        var registry = host.GetRequiredService<IShellRegistry>();
        var dataSource = host.GetRequiredService<DynamicShellEndpointDataSource>();
        var pipelines = host.GetRequiredService<ShellMiddlewarePipelineRegistry>();
        var shellId = new ShellId("provisional");
        var generationOne = await registry.ActivateAsync(shellId.Name);
        var reloadTask = registry.ReloadAsync(shellId.Name);

        await slowSubscriber.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        var visibleEndpoint = Assert.Single(dataSource.Endpoints);
        var visibleGeneration = visibleEndpoint.Metadata.GetMetadata<ShellEndpointMetadata>()!.Generation;
        var response = new ShellMiddlewareTests.FireableResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(response);
        context.SetEndpoint(visibleEndpoint);
        var middleware = ShellMiddlewareTests.CreateMiddleware(
            _ => Task.CompletedTask,
            registry: registry,
            endpointDataSource: dataSource,
            pipelineRegistry: pipelines);

        await middleware.InvokeAsync(context);
        await response.FireOnCompletedAsync();
        slowSubscriber.Release();
        var reload = await reloadTask;

        Assert.Equal(generationOne.Descriptor.Generation, visibleGeneration);
        Assert.Equal("stable", context.Items["pipeline-marker"]);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Null(reload.Error);
        Assert.Equal(2, dataSource.Endpoints.Single().Metadata.GetMetadata<ShellEndpointMetadata>()!.Generation);
    }

    [Fact(DisplayName = "Commit conflict restores the prior registry generation and discards the staged pipeline")]
    public async Task Reload_CommitConflict_RestoresPriorActiveAndAllEntries()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<HostConflictOnSecondCommitParticipant>();
        services.AddSingleton<IShellGenerationActivationParticipant>(sp =>
            sp.GetRequiredService<HostConflictOnSecondCommitParticipant>());
        services.AddCShellsAspNetCore(cshells => cshells
            .WithAssemblyContaining<RollbackStableFeature>()
            .AddShell("commit-conflict", shell => shell.WithFeature<RollbackStableFeature>()));

        await using var host = services.BuildServiceProvider();
        CaptureRoutingInfrastructure(host);
        var registry = host.GetRequiredService<IShellRegistry>();
        var dataSource = host.GetRequiredService<DynamicShellEndpointDataSource>();
        var pipelines = host.GetRequiredService<ShellMiddlewarePipelineRegistry>();
        var shellId = new ShellId("commit-conflict");
        var generationOne = await registry.ActivateAsync(shellId.Name);

        var reload = await registry.ReloadAsync(shellId.Name);

        Assert.IsType<ShellGenerationActivationException>(reload.Error);
        Assert.Null(reload.NewShell);
        Assert.Same(generationOne, registry.GetActive(shellId.Name));
        Assert.Equal([generationOne], registry.GetAll(shellId.Name));
        AssertGeneration(dataSource, expectedGeneration: 1);
        await AssertPipelineMarkerAsync(pipelines, shellId, generation: 1, "stable");
        Assert.Null(pipelines.Get(shellId, generation: 2, _ => Task.CompletedTask));
    }

    [Fact(DisplayName = "A later participant rejection rolls committed routes and pipeline back to the prior generation")]
    public async Task Reload_LaterParticipantRejectsCommit_RestoresPriorRoutesAndPipeline()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShellsAspNetCore(cshells => cshells
            .WithAssemblyContaining<RollbackStableFeature>()
            .AddShell("later-rejection", shell => shell.WithFeature<RollbackStableFeature>()));
        services.AddSingleton<IShellGenerationActivationParticipant, RejectSecondGenerationCommitParticipant>();

        await using var host = services.BuildServiceProvider();
        CaptureRoutingInfrastructure(host);
        var registry = host.GetRequiredService<IShellRegistry>();
        var dataSource = host.GetRequiredService<DynamicShellEndpointDataSource>();
        var pipelines = host.GetRequiredService<ShellMiddlewarePipelineRegistry>();
        var shellId = new ShellId("later-rejection");
        var generationOne = await registry.ActivateAsync(shellId.Name);

        var reload = await registry.ReloadAsync(shellId.Name);

        Assert.IsType<ShellGenerationActivationException>(reload.Error);
        Assert.Null(reload.NewShell);
        Assert.Same(generationOne, registry.GetActive(shellId.Name));
        Assert.Equal([generationOne], registry.GetAll(shellId.Name));
        AssertGeneration(dataSource, expectedGeneration: 1);
        await AssertPipelineMarkerAsync(pipelines, shellId, generation: 1, "stable");
        Assert.Null(pipelines.Get(shellId, generation: 2, _ => Task.CompletedTask));
    }

    [Fact(DisplayName = "Another shell can drain and release endpoints while a publication transaction is pending")]
    public async Task PendingCommit_OtherShellDrain_RemovesEndpointsAndReleasesReferences()
    {
        var blocker = new BlockingActivationParticipant("cleanup-b", generation: 1);
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShellsAspNetCore(cshells => cshells
            .WithAssemblyContaining<CleanupFeatureA>()
            .AddShell("cleanup-a", shell => shell.WithFeature<CleanupFeatureA>())
            .AddShell("cleanup-b", shell => shell.WithFeature<CleanupFeatureB>()));
        services.AddSingleton<IShellGenerationActivationParticipant>(blocker);

        await using var host = services.BuildServiceProvider();
        CaptureRoutingInfrastructure(host);
        var registry = host.GetRequiredService<IShellRegistry>();
        var dataSource = host.GetRequiredService<DynamicShellEndpointDataSource>();
        var shellA = await registry.ActivateAsync("cleanup-a");
        var endpointReference = CaptureEndpointReference(dataSource, new ShellId("cleanup-a"));
        var activateB = Task.Run(() => registry.ActivateAsync("cleanup-b"));

        await blocker.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var drain = await registry.DrainAsync(shellA);
            await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.DoesNotContain(dataSource.Endpoints, endpoint =>
                endpoint.Metadata.GetMetadata<ShellEndpointMetadata>()?.ShellId.Equals(new ShellId("cleanup-a")) == true);
            Assert.Contains(dataSource.Endpoints, endpoint =>
                endpoint.Metadata.GetMetadata<ShellEndpointMetadata>()?.ShellId.Equals(new ShellId("cleanup-b")) == true);
            await AssertReferenceCollectedAsync(endpointReference, "drained cleanup-a endpoint");
        }
        finally
        {
            blocker.Release();
        }

        await activateB.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "A pending candidate can drain and dispose without breaking rollback restoration")]
    public async Task PendingCommit_SameShellCandidateDrain_RestoresPriorGenerationWithoutStaleRoutes()
    {
        var blocker = new BlockingActivationParticipant("cleanup-same", generation: 2, rejectAfterRelease: true);
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShellsAspNetCore(cshells => cshells
            .WithAssemblyContaining<CleanupFeatureA>()
            .AddShell("cleanup-same", shell => shell.WithFeature<CleanupFeatureA>()));
        services.AddSingleton<IShellGenerationActivationParticipant>(blocker);

        await using var host = services.BuildServiceProvider();
        CaptureRoutingInfrastructure(host);
        var registry = host.GetRequiredService<IShellRegistry>();
        var dataSource = host.GetRequiredService<DynamicShellEndpointDataSource>();
        var generationOne = await registry.ActivateAsync("cleanup-same");
        var reloadTask = Task.Run(() => registry.ReloadAsync("cleanup-same"));

        await blocker.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var candidate = registry.GetActive("cleanup-same")!;
            Assert.Equal(2, candidate.Descriptor.Generation);
            var drain = await registry.DrainAsync(candidate);
            await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(ShellLifecycleState.Disposed, candidate.State);
        }
        finally
        {
            blocker.Release();
        }

        var reload = await reloadTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsType<ShellGenerationActivationException>(reload.Error);
        Assert.Same(generationOne, registry.GetActive("cleanup-same"));
        AssertGeneration(dataSource, expectedGeneration: 1);
        Assert.Single(dataSource.Endpoints);
    }

    [Fact(DisplayName = "A rejected candidate does not restore a prior generation that drained during commit")]
    public async Task PendingCommit_PriorGenerationDrainsThenCandidateRejects_RecoversWithoutDisposedRoutes()
    {
        const string shellName = "cleanup-prior";
        var blocker = new BlockingActivationParticipant(shellName, generation: 2, rejectAfterRelease: true);
        await using var host = CreateStableShellHost(shellName, blocker);
        var registry = host.GetRequiredService<IShellRegistry>();
        var dataSource = host.GetRequiredService<DynamicShellEndpointDataSource>();
        var generationOne = await registry.ActivateAsync(shellName);
        var reloadTask = Task.Run(() => registry.ReloadAsync(shellName));

        await blocker.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var drain = await registry.DrainAsync(generationOne);
            await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(ShellLifecycleState.Disposed, generationOne.State);

            var rollbackSnapshot = new TaskCompletionSource<IReadOnlyList<Endpoint>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = dataSource.GetChangeToken().RegisterChangeCallback(
                _ => rollbackSnapshot.TrySetResult(dataSource.Endpoints.ToArray()),
                null);

            blocker.Release();
            var reload = await reloadTask.WaitAsync(TimeSpan.FromSeconds(5));
            var observed = await rollbackSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.IsType<ShellGenerationActivationException>(reload.Error);
            Assert.Null(reload.NewShell);
            Assert.Empty(observed);
            Assert.Empty(dataSource.Endpoints);
            Assert.Null(registry.GetActive(shellName));
            Assert.DoesNotContain(generationOne, registry.GetAll(shellName));
            var recovered = await AssertRecoveredGenerationServesAsync(host, shellName);
            Assert.Equal(3, recovered.Descriptor.Generation);
        }
        finally
        {
            blocker.Release();
        }
    }

    [Fact(DisplayName = "A candidate that drains during commit is rejected and the prior generation remains live")]
    public async Task PendingCommit_CandidateDrainsThenParticipantsAccept_DoesNotReturnDisposedGeneration()
    {
        const string shellName = "cleanup-candidate";
        var blocker = new BlockingActivationParticipant(shellName, generation: 2);
        await using var host = CreateStableShellHost(shellName, blocker);

        var registry = host.GetRequiredService<IShellRegistry>();
        var dataSource = host.GetRequiredService<DynamicShellEndpointDataSource>();
        var generationOne = await registry.ActivateAsync(shellName);
        var reloadTask = Task.Run(() => registry.ReloadAsync(shellName));

        await blocker.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        var candidate = (IShell?)null;
        try
        {
            candidate = Assert.IsAssignableFrom<IShell>(registry.GetActive(shellName));
            Assert.Equal(2, candidate.Descriptor.Generation);
            var drain = await registry.DrainAsync(candidate);
            await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(ShellLifecycleState.Disposed, candidate.State);
        }
        finally
        {
            blocker.Release();
        }

        var reload = await reloadTask.WaitAsync(TimeSpan.FromSeconds(5));
        var rejectedCandidate = Assert.IsAssignableFrom<IShell>(candidate);

        Assert.IsType<ShellGenerationActivationException>(reload.Error);
        Assert.Null(reload.NewShell);
        Assert.DoesNotContain(rejectedCandidate, registry.GetAll(shellName));
        Assert.DoesNotContain(registry.GetActiveShells(), shell => shell.State == ShellLifecycleState.Disposed);
        Assert.Same(generationOne, registry.GetActive(shellName));
        AssertGeneration(dataSource, expectedGeneration: 1);
        var recovered = await AssertRecoveredGenerationServesAsync(host, shellName);
        Assert.Same(generationOne, recovered);
    }

    [Fact(DisplayName = "A candidate drained during completion leaves the slot safe for reactivation")]
    public async Task Reload_CandidateDrainsDuringParticipantCompletion_ReactivatesWithoutZombieGeneration()
    {
        const string shellName = "cleanup-completion";
        var drainer = new DrainCandidateOnCompletionParticipant();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShellsAspNetCore(cshells => cshells
            .WithAssemblyContaining<RollbackStableFeature>()
            .AddShell(shellName, shell => shell.WithFeature<RollbackStableFeature>()));
        services.AddSingleton<IShellGenerationActivationParticipant>(drainer);

        await using var host = services.BuildServiceProvider();
        CaptureRoutingInfrastructure(host);
        var registry = host.GetRequiredService<IShellRegistry>();
        var dataSource = host.GetRequiredService<DynamicShellEndpointDataSource>();
        drainer.AttachRegistry(registry);
        var generationOne = await registry.ActivateAsync(shellName);

        var reload = await registry.ReloadAsync(shellName);

        Assert.IsType<ShellGenerationActivationException>(reload.Error);
        Assert.Null(reload.NewShell);
        Assert.NotNull(drainer.DrainedShell);
        Assert.Equal(ShellLifecycleState.Disposed, drainer.DrainedShell.State);
        Assert.NotEqual(ShellLifecycleState.Active, generationOne.State);
        Assert.Null(registry.GetActive(shellName));
        Assert.Empty(dataSource.Endpoints);
        var recovered = await AssertRecoveredGenerationServesAsync(host, shellName);
        Assert.Equal(3, recovered.Descriptor.Generation);
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
        for (var attempt = 0; attempt < 100 && loadContext.IsAlive; attempt++)
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CaptureEndpointReference(
        DynamicShellEndpointDataSource dataSource,
        ShellId shellId) =>
        new(dataSource.Endpoints.Single(endpoint =>
            endpoint.Metadata.GetMetadata<ShellEndpointMetadata>()?.ShellId.Equals(shellId) == true));

    private static async Task AssertReferenceCollectedAsync(WeakReference reference, string description)
    {
        for (var attempt = 0; attempt < 100 && reference.IsAlive; attempt++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            await Task.Yield();
        }

        Assert.False(reference.IsAlive, $"The {description} remained rooted after lifecycle cleanup.");
    }

    private static void CaptureRoutingInfrastructure(IServiceProvider services)
    {
        services.GetRequiredService<EndpointRouteBuilderAccessor>().EndpointRouteBuilder = new TestEndpointRouteBuilder(services);
        services.GetRequiredService<ApplicationBuilderAccessor>().ApplicationBuilder = new ApplicationBuilder(services);
    }

    private static ServiceProvider CreateStableShellHost(
        string shellName,
        IShellGenerationActivationParticipant blocker)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShellsAspNetCore(cshells => cshells
            .WithAssemblyContaining<RollbackStableFeature>()
            .AddShell(shellName, shell => shell.WithFeature<RollbackStableFeature>()));
        services.AddSingleton<IShellGenerationActivationParticipant>(blocker);

        var host = services.BuildServiceProvider();
        CaptureRoutingInfrastructure(host);
        return host;
    }

    private static void AssertGeneration(DynamicShellEndpointDataSource dataSource, int expectedGeneration)
    {
        var endpoint = Assert.Single(dataSource.Endpoints);
        Assert.Equal(expectedGeneration, endpoint.Metadata.GetMetadata<ShellEndpointMetadata>()!.Generation);
    }

    private static async Task AssertPipelineMarkerAsync(
        ShellMiddlewarePipelineRegistry pipelines,
        ShellId shellId,
        int generation,
        string expectedMarker)
    {
        var pipeline = pipelines.Get(shellId, generation, _ => Task.CompletedTask);
        Assert.NotNull(pipeline);
        var context = new DefaultHttpContext();
        await pipeline!(context);
        Assert.Equal(expectedMarker, context.Items["pipeline-marker"]);
    }

    private static async Task<IShell> AssertRecoveredGenerationServesAsync(
        IServiceProvider host,
        string shellName)
    {
        var registry = host.GetRequiredService<IShellRegistry>();
        var dataSource = host.GetRequiredService<DynamicShellEndpointDataSource>();
        var pipelines = host.GetRequiredService<ShellMiddlewarePipelineRegistry>();
        var recovered = await registry.GetOrActivateAsync(shellName);
        Assert.Equal(ShellLifecycleState.Active, recovered.State);
        Assert.Same(recovered, registry.GetActive(shellName));
        Assert.DoesNotContain(registry.GetAll(shellName), shell => shell.State == ShellLifecycleState.Disposed);

        var endpoint = Assert.Single(dataSource.Endpoints);
        Assert.Equal(recovered.Descriptor.Generation,
            endpoint.Metadata.GetMetadata<ShellEndpointMetadata>()!.Generation);
        var response = new ShellMiddlewareTests.FireableResponseFeature();
        var context = new DefaultHttpContext { RequestServices = host };
        context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(response);
        context.SetEndpoint(endpoint);
        var middleware = ShellMiddlewareTests.CreateMiddleware(
            _ => Task.CompletedTask,
            registry: registry,
            endpointDataSource: dataSource,
            pipelineRegistry: pipelines);

        await middleware.InvokeAsync(context);
        await response.FireOnCompletedAsync();

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("stable", context.Items["pipeline-marker"]);
        return recovered;
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

[ShellFeature("RollbackStable")]
public sealed class RollbackStableFeature : IWebShellFeature, IMiddlewareShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) =>
        endpoints.MapGet("/rollback", () => Results.Ok());

    public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment) =>
        app.Use(next => context =>
        {
            context.Items["pipeline-marker"] = "stable";
            return next(context);
        });
}

[ShellFeature("FailOnSecondComposition")]
public sealed class FailOnSecondCompositionFeature(MiddlewareCompositionAttempts attempts) : IWebShellFeature, IMiddlewareShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) =>
        endpoints.MapGet("/composition", () => Results.Ok());

    public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment)
    {
        if (attempts.Increment() == 2)
            throw new InvalidOperationException("second generation middleware composition failed");

        app.Use(next => context =>
        {
            context.Items["pipeline-marker"] = "composition";
            return next(context);
        });
    }
}

public sealed class MiddlewareCompositionAttempts
{
    private int attempts;

    public int Increment() => Interlocked.Increment(ref attempts);
}

public sealed class RejectSecondGenerationSubscriber : IShellLifecycleSubscriber
{
    public Task OnStateChangedAsync(
        IShell shell,
        ShellLifecycleState previous,
        ShellLifecycleState current,
        CancellationToken cancellationToken = default) =>
        current == ShellLifecycleState.Active && shell.Descriptor.Generation == 2
            ? throw new ShellGenerationActivationException(
                shell.Descriptor,
                new InvalidOperationException("later subscriber rejected generation two"))
            : Task.CompletedTask;
}

public sealed class SlowSecondGenerationSubscriber : IShellLifecycleSubscriber
{
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => entered.Task;

    public void Release() => release.TrySetResult();

    public async Task OnStateChangedAsync(
        IShell shell,
        ShellLifecycleState previous,
        ShellLifecycleState current,
        CancellationToken cancellationToken = default)
    {
        if (current != ShellLifecycleState.Active || shell.Descriptor.Generation != 2)
            return;

        entered.TrySetResult();
        await release.Task.WaitAsync(cancellationToken);
    }
}

public sealed class HostConflictOnSecondCommitParticipant(EndpointRouteBuilderAccessor routeBuilderAccessor) :
    IShellGenerationActivationParticipant
{
    public Task PrepareAsync(IShell shell, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public void Commit(IShell shell)
    {
        if (shell.Descriptor.Generation != 2)
            return;

        var hostEndpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/rollback"),
            order: 0,
            new EndpointMetadataCollection(new HttpMethodMetadata(["GET"])),
            "Late host conflict");
        routeBuilderAccessor.EndpointRouteBuilder!.DataSources.Add(
            new DefaultEndpointDataSource(hostEndpoint));
    }

    public void Complete(IShell shell)
    {
    }

    public void Rollback(IShell shell)
    {
    }
}

public sealed class RejectSecondGenerationCommitParticipant : IShellGenerationActivationParticipant
{
    public Task PrepareAsync(IShell shell, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public void Commit(IShell shell)
    {
        if (shell.Descriptor.Generation == 2)
            throw new InvalidOperationException("later participant rejected generation two");
    }

    public void Complete(IShell shell)
    {
    }

    public void Rollback(IShell shell)
    {
    }
}

[ShellFeature("CleanupFeatureA")]
public sealed class CleanupFeatureA : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) =>
        endpoints.MapGet("/cleanup/a", () => Results.Ok());
}

[ShellFeature("CleanupFeatureB")]
public sealed class CleanupFeatureB : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) =>
        endpoints.MapGet("/cleanup/b", () => Results.Ok());
}

public sealed class BlockingActivationParticipant(
    string shellName,
    int generation,
    bool rejectAfterRelease = false) : IShellGenerationActivationParticipant
{
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => entered.Task;

    public Task PrepareAsync(IShell shell, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public void Commit(IShell shell)
    {
        if (!string.Equals(shell.Descriptor.Name, shellName, StringComparison.OrdinalIgnoreCase)
            || shell.Descriptor.Generation != generation)
            return;

        entered.TrySetResult();
        release.Task.GetAwaiter().GetResult();
        if (rejectAfterRelease)
            throw new InvalidOperationException("blocked candidate rejected after cleanup");
    }

    public void Complete(IShell shell)
    {
    }

    public void Rollback(IShell shell)
    {
    }

    public void Release() => release.TrySetResult();
}

internal sealed class DrainCandidateOnCompletionParticipant : IShellGenerationActivationParticipant
{
    private IShellRegistry? registry;

    internal IShell? DrainedShell { get; private set; }

    internal void AttachRegistry(IShellRegistry value) =>
        registry = value;

    public Task PrepareAsync(IShell shell, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public void Commit(IShell shell)
    {
    }

    public void Complete(IShell shell)
    {
        if (shell.Descriptor.Generation != 2)
            return;

        DrainedShell = shell;
        var currentRegistry = registry ?? throw new InvalidOperationException("The registry was not attached.");
        var drain = currentRegistry.DrainAsync(shell).GetAwaiter().GetResult();
        drain.WaitAsync().GetAwaiter().GetResult();
    }

    public void Rollback(IShell shell)
    {
    }
}
