using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CShells.Tests.Integration.Lifecycle;

public sealed class ShellGenerationBuildLeaseTests
{
    [Fact]
    public void GenerationNumberRejectsExhaustionBeforeNarrowingToDescriptorInteger()
    {
        Assert.Equal(int.MaxValue, ShellRegistry.NextGenerationNumber((long)int.MaxValue - 1, "generation-boundary"));
        Assert.Throws<InvalidOperationException>(() => ShellRegistry.NextGenerationNumber(int.MaxValue, "generation-boundary"));
    }

    [Fact]
    public async Task ParticipantsReceiveReservedIdentityAndSelectedSnapshotBeforeFeatureConstruction()
    {
        var events = new List<string>();
        var participant = new RecordingParticipant(events);
        BuildLeaseTestEvents.Events = events;

        await using var host = BuildHost(participant, cshells => cshells
            .WithAssemblyProvider(new RecordingAssemblyProvider(typeof(LeaseOrderFeature).Assembly, events))
            .AddShell("lease-order", shell => shell.WithFeature<LeaseOrderFeature>()));
        var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync("lease-order");

        Assert.Equal(["begin:participant:1", "catalog", "snapshot:participant:1", "feature"], events);
        Assert.Equal(new ShellId("lease-order"), participant.Context!.ShellId);
        Assert.Equal("lease-order", participant.Context.Descriptor.Name);
        Assert.Equal(1, participant.Context.Descriptor.Generation);
        Assert.Same(participant.Snapshot, participant.Lease!.Snapshot);
        var selectedCatalogSnapshot = await host.GetRequiredService<IRuntimeFeatureCatalog>().GetSnapshotAsync();
        Assert.Same(selectedCatalogSnapshot, participant.Lease.Snapshot);
        Assert.Null(shell.ServiceProvider.GetService<IShellGenerationBuildParticipant>());

        var drain = await host.GetRequiredService<IShellRegistry>().DrainAsync(shell);
        await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, participant.Lease.DisposeCount);
    }

    [Fact]
    public async Task DescriptorPreservesCaseDistinctOpaqueMetadataKeys()
    {
        var participant = new RecordingParticipant([]);
        var blueprint = new MetadataBlueprint();
        await using var host = BuildHost(participant, cshells => cshells.AddBlueprint(blueprint));

        var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync(blueprint.Name);

        Assert.Equal("upper", participant.Context!.Descriptor.Metadata["Foo"]);
        Assert.Equal("lower", participant.Context.Descriptor.Metadata["foo"]);
        var drain = await host.GetRequiredService<IShellRegistry>().DrainAsync(shell);
        await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MetadataIsSnapshottedBeforeComposeMutatesBlueprint()
    {
        var participant = new RecordingParticipant([]);
        var blueprint = new MutatingMetadataBlueprint();
        await using var host = BuildHost(participant, cshells => cshells.AddBlueprint(blueprint));

        var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync(blueprint.Name);

        Assert.Equal("before", participant.Context!.Descriptor.Metadata["revision"]);
        Assert.Equal("after", blueprint.Metadata["revision"]);
        var drain = await host.GetRequiredService<IShellRegistry>().DrainAsync(shell);
        await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CompositionFailureOrNameMismatchDoesNotBeginParticipants()
    {
        var participant = new RecordingParticipant([]);

        await using var host = BuildHost(participant, cshells =>
        {
            cshells.AddBlueprint(new InvalidCompositionBlueprint("compose-failure", throwOnCompose: true));
            cshells.AddBlueprint(new InvalidCompositionBlueprint("name-mismatch", throwOnCompose: false));
        });
        var registry = host.GetRequiredService<IShellRegistry>();

        await Assert.ThrowsAsync<ApplicationException>(() => registry.ActivateAsync("compose-failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => registry.ActivateAsync("name-mismatch"));

        Assert.Null(participant.Context);
        Assert.Null(participant.Lease);
    }

    [Fact]
    public async Task CancellationAfterCompositionStopsBeforeParticipantAcquisition()
    {
        using var cancellation = new CancellationTokenSource();
        var participant = new RecordingParticipant([]);
        var blueprint = new InvalidCompositionBlueprint("cancel-before-begin", cancellation, throwOnCompose: false);
        await using var host = BuildHost(participant, cshells => cshells.AddBlueprint(blueprint));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            host.GetRequiredService<IShellRegistry>().ActivateAsync(blueprint.Name, cancellation.Token));

        Assert.Null(participant.Context);
        Assert.Null(participant.Lease);
    }

    [Fact]
    public async Task FeatureConstructionFailureReleasesAcquiredLease()
    {
        var participant = new RecordingParticipant([]);
        await using var host = BuildHost(participant, cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("feature-failure", shell => shell.WithFeature<ThrowingFeature>()));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.GetRequiredService<IShellRegistry>().ActivateAsync("feature-failure"));

        Assert.IsType<ApplicationException>(error.InnerException);
        Assert.Equal(1, participant.Lease!.DisposeCount);
        Assert.Null(host.GetRequiredService<IShellRegistry>().GetActive("feature-failure"));
    }

    [Fact]
    public async Task BeginFailureIsPreservedAndPreviouslyAcquiredLeaseIsReleased()
    {
        var events = new List<string>();
        var failure = new ApplicationException("begin failed");
        var first = new RecordingParticipant(events, "first", releaseFailure: new ApplicationException("lease dispose failed"));
        var second = new ThrowingBeginParticipant(failure);
        var third = new RecordingParticipant(events, "third");
        await using var host = BuildHost([first, second, third], cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("begin-failure", _ => { }), services =>
            services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ShellRegistry>>(new ThrowingErrorLogger<ShellRegistry>()));
        var registry = Assert.IsType<ShellRegistry>(host.GetRequiredService<IShellRegistry>());

        var error = await Assert.ThrowsAsync<ApplicationException>(() =>
            registry.ActivateAsync("begin-failure"));

        Assert.Same(failure, error);
        Assert.Equal(["begin:first:1", "dispose:first"], events);
        Assert.Null(third.Context);
        Assert.Equal(1, registry.RetainedBuildLeaseCount);
    }

    [Fact]
    public async Task CatalogInitializationFailureReleasesLeaseAndPreservesCatalogError()
    {
        var participant = new RecordingParticipant([]);
        var failure = new ApplicationException("catalog initialization failed");
        await using var host = BuildHost(participant, cshells => cshells
            .WithAssemblyProvider(new ThrowingAssemblyProvider(failure))
            .AddShell("catalog-failure", _ => { }));

        var error = await Assert.ThrowsAsync<ApplicationException>(() =>
            host.GetRequiredService<IShellRegistry>().ActivateAsync("catalog-failure"));

        Assert.Same(failure, error);
        Assert.Null(participant.Snapshot);
        Assert.Equal(1, participant.Lease!.DisposeCount);
    }

    [Fact]
    public async Task CancellationDuringSnapshotCallbackStopsBeforeFeatureConstructionAndReleasesLeases()
    {
        using var cancellation = new CancellationTokenSource();
        var events = new List<string>();
        var first = new RecordingParticipant(events, "first", onSnapshot: cancellation.Cancel);
        var second = new RecordingParticipant(events, "second");
        await using var host = BuildHost([first, second], cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("snapshot-cancel", shell => shell.WithFeature<ThrowingFeature>()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            host.GetRequiredService<IShellRegistry>().ActivateAsync("snapshot-cancel", cancellation.Token));

        Assert.Null(second.Snapshot);
        Assert.Equal(1, first.Lease!.DisposeCount);
        Assert.Equal(1, second.Lease!.DisposeCount);
        Assert.Equal(["begin:first:1", "begin:second:1", "snapshot:first:1", "dispose:second", "dispose:first"], events);
    }

    [Fact]
    public async Task DistinctShellNamesCanRunBuildCallbacksConcurrently()
    {
        var participant = new BeginBarrierParticipant();
        await using var host = BuildHost(participant, cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("parallel-one", _ => { })
            .AddShell("parallel-two", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();

        var activationOne = registry.ActivateAsync("parallel-one");
        var activationTwo = registry.ActivateAsync("parallel-two");
        try
        {
            await participant.BothEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, participant.BeginCount);
        }
        finally
        {
            participant.Allow.TrySetResult();
        }

        var shells = await Task.WhenAll(activationOne, activationTwo).WaitAsync(TimeSpan.FromSeconds(5));
        foreach (var shell in shells)
        {
            var drain = await registry.DrainAsync(shell);
            await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task ReloadOverlapsNewActiveGenerationWithOldGenerationTeardown()
    {
        var participant = new GenerationLeaseParticipant();
        await using var host = BuildHost(participant, cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("overlapping-reload", shell => shell.WithFeature<BlockingDisposalFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var oldShell = await registry.ActivateAsync("overlapping-reload");
        var oldProbe = oldShell.ServiceProvider.GetRequiredService<BlockingShellDisposal>();
        var oldLease = participant.Leases[1];

        var reload = await registry.ReloadAsync("overlapping-reload").WaitAsync(TimeSpan.FromSeconds(5));
        var newShell = Assert.IsAssignableFrom<IShell>(reload.NewShell);
        var newLease = participant.Leases[2];
        BlockingShellDisposal? newProbe = null;

        try
        {
            newProbe = newShell.ServiceProvider.GetRequiredService<BlockingShellDisposal>();
            await oldProbe.DisposeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(newShell, registry.GetActive("overlapping-reload"));
            Assert.Equal(0, oldLease.DisposeCount);
            Assert.Equal(0, newLease.DisposeCount);

            oldProbe.AllowDispose.TrySetResult();
            await reload.Drain!.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, oldLease.DisposeCount);
            Assert.Equal(0, newLease.DisposeCount);
        }
        finally
        {
            oldProbe.AllowDispose.TrySetResult();
            newProbe?.AllowDispose.TrySetResult();
        }

        var newDrain = await registry.DrainAsync(newShell);
        await newDrain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, newLease.DisposeCount);
    }

    [Fact]
    public async Task ActivationFailureRemainsPrimaryWhenRollbackLoggingThrows()
    {
        var participant = new RecordingParticipant([]);
        var commitFailure = new ApplicationException("commit failed");
        await using var host = BuildHost(participant, cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("activation-failure", shell => shell.WithFeature<ActivationRollbackDisposeFailureFeature>()), services =>
            {
                services.AddSingleton<IShellGenerationActivationParticipant>(new ThrowingActivationParticipant(commitFailure, failProviderDispose: true));
                services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ShellRegistry>>(new ThrowingErrorLogger<ShellRegistry>());
            });
        var registry = Assert.IsType<ShellRegistry>(host.GetRequiredService<IShellRegistry>());

        var error = await Assert.ThrowsAsync<ShellGenerationActivationException>(() =>
            registry.ActivateAsync("activation-failure"));

        Assert.Same(commitFailure, error.InnerException);
        Assert.Equal(0, participant.Lease!.DisposeCount);
        Assert.Equal(1, registry.RetainedBuildLeaseCount);
        Assert.Null(registry.GetActive("activation-failure"));
    }

    [Fact]
    public async Task InitializerFailureReleasesLeaseAfterPartialProviderDisposalWithoutLifecycleTransitions()
    {
        var participant = new RecordingParticipant([]);
        var subscriber = new RecordingLifecycleSubscriber();
        await using var host = BuildHost(participant, cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("initializer-cleanup", shell => shell.WithFeature<InitializerFailureFeature>()), services =>
            services.AddSingleton<IShellLifecycleSubscriber>(subscriber));
        var registry = host.GetRequiredService<IShellRegistry>();

        await Assert.ThrowsAsync<ApplicationException>(() => registry.ActivateAsync("initializer-cleanup"));

        Assert.Equal(1, participant.Lease!.DisposeCount);
        Assert.Empty(subscriber.Transitions);
        Assert.Empty(registry.GetAll("initializer-cleanup"));
    }

    [Fact]
    public async Task PartialProviderDisposalFailurePreservesInitializerErrorAndRetainsLease()
    {
        var participant = new RecordingParticipant([]);
        var subscriber = new RecordingLifecycleSubscriber();
        await using var host = BuildHost(participant, cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("initializer-dispose-failure", shell => shell.WithFeature<InitializerFailureDisposeFeature>()), services =>
            {
                services.AddSingleton<IShellLifecycleSubscriber>(subscriber);
                services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ShellRegistry>>(new ThrowingErrorLogger<ShellRegistry>());
            });
        var registry = Assert.IsType<ShellRegistry>(host.GetRequiredService<IShellRegistry>());

        var error = await Assert.ThrowsAsync<ApplicationException>(() => registry.ActivateAsync("initializer-dispose-failure"));

        Assert.Equal("initializer failed", error.Message);
        Assert.Equal(0, participant.Lease!.DisposeCount);
        Assert.Equal(1, registry.RetainedBuildLeaseCount);
        Assert.Empty(subscriber.Transitions);
        Assert.Empty(registry.GetAll("initializer-dispose-failure"));
    }

    [Fact]
    public async Task SnapshotCallbackFailureSkipsLaterCallbacksAndReleasesAllAcquiredLeasesInReverseOrder()
    {
        var events = new List<string>();
        var first = new RecordingParticipant(events, "first", callbackFailure: new ApplicationException("snapshot failed"));
        var second = new RecordingParticipant(events, "second");

        await using var host = BuildHost([first, second], cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("lease-failure", _ => { }));

        var error = await Assert.ThrowsAsync<ApplicationException>(() =>
            host.GetRequiredService<IShellRegistry>().ActivateAsync("lease-failure"));

        Assert.Same(first.Lease!.CallbackFailure, error);
        Assert.Equal(["begin:first:1", "begin:second:1", "snapshot:first:1", "dispose:second", "dispose:first"], events);
        Assert.Equal(1, first.Lease.DisposeCount);
        Assert.Equal(1, second.Lease!.DisposeCount);
    }

    [Fact]
    public async Task LeaseRemainsOwnedUntilSlowProviderTeardownFinishesAfterDisposedNotification()
    {
        var participant = new RecordingParticipant([]);

        await using var host = BuildHost(participant, cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("lease-dispose", shell => shell.WithFeature<BlockingDisposalFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("lease-dispose");
        var probe = shell.ServiceProvider.GetRequiredService<BlockingShellDisposal>();

        var drain = await registry.DrainAsync(shell);
        try
        {
            await probe.DisposeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(ShellLifecycleState.Disposed, shell.State);
            Assert.Equal(0, participant.Lease!.DisposeCount);
        }
        finally
        {
            probe.AllowDispose.TrySetResult();
        }

        await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, participant.Lease.DisposeCount);
    }

    [Fact]
    public async Task SelectedOldSnapshotRemainsPinnedAfterNewCommitAndOtherOldShellsDrainBeforeInitializerCompletes()
    {
        CatalogSnapshotGate.Reset();
        CandidateInitializerGate.Reset();
        var participant = new CatalogPinParticipant();
        await using var host = BuildHost(participant, cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("pin-main", shell => shell.WithFeature<BlockingCandidateFeature>())
            .AddShell("pin-sibling", shell => shell.WithFeature<BlockingCandidateFeature>()));
        var registry = host.GetRequiredService<IShellRegistry>();
        var catalog = host.GetRequiredService<IRuntimeFeatureCatalog>();
        var originalSnapshot = await catalog.GetSnapshotAsync();
        CandidateInitializerGate.ReadPinCount = () => participant.PinCount(originalSnapshot.Generation);
        var oldMain = await registry.ActivateAsync("pin-main");
        var sibling = await registry.ActivateAsync("pin-sibling");
        Task<ReloadResult>? reloadTask = null;

        try
        {
            reloadTask = registry.ReloadAsync("pin-main");
            await CatalogSnapshotGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(3, participant.PinCount(originalSnapshot.Generation));
            Assert.Equal(0, CandidateInitializerGate.InitializerCalls);

            var oldMainDrain = await registry.DrainAsync(oldMain);
            var siblingDrain = await registry.DrainAsync(sibling);
            await Task.WhenAll(
                oldMainDrain.WaitAsync(),
                siblingDrain.WaitAsync()).WaitAsync(TimeSpan.FromSeconds(5));

            await catalog.RefreshAsync();
            var committedSnapshot = await catalog.GetSnapshotAsync();
            Assert.True(committedSnapshot.Generation > originalSnapshot.Generation);
            Assert.Equal(1, participant.PinCount(originalSnapshot.Generation));
            Assert.Equal(0, CandidateInitializerGate.InitializerCalls);

            CatalogSnapshotGate.Allow.TrySetResult();
            var reload = await reloadTask.WaitAsync(TimeSpan.FromSeconds(5));
            var candidate = Assert.IsAssignableFrom<IShell>(reload.NewShell);
            Assert.Equal(originalSnapshot.Generation, participant.SelectedGeneration(candidate.Descriptor.Generation));
            Assert.Equal(1, CandidateInitializerGate.InitializerCalls);
            Assert.Equal(1, CandidateInitializerGate.PinCountAtInitializer);
            Assert.Equal(1, participant.PinCount(originalSnapshot.Generation));

            var candidateDrain = await registry.DrainAsync(candidate);
            await candidateDrain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, participant.PinCount(originalSnapshot.Generation));
        }
        finally
        {
            CatalogSnapshotGate.Allow.TrySetResult();
            if (reloadTask is not null)
            {
                try
                {
                    await reloadTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // Preserve the assertion failure; the host's normal teardown handles any active shell.
                }
            }

            CandidateInitializerGate.Reset();
        }
    }

    [Fact]
    public async Task FailedLeaseReleaseRetainsOnlyUnresolvedLeaseAndAttemptsEarlierLeases()
    {
        var events = new List<string>();
        var firstFailure = new ApplicationException("first release failed");
        var secondFailure = new ApplicationException("second release failed");
        var first = new RecordingParticipant(events, "first", releaseFailure: firstFailure);
        var second = new RecordingParticipant(events, "second", releaseFailure: secondFailure);
        var third = new RecordingParticipant(events, "third");

        await using var host = BuildHost([first, second, third], cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("lease-retained", _ => { }));
        var registry = Assert.IsType<ShellRegistry>(host.GetRequiredService<IShellRegistry>());
        var shell = await registry.ActivateAsync("lease-retained");

        var drain = await registry.DrainAsync(shell);
        var aggregate = await Assert.ThrowsAsync<AggregateException>(() => drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(["dispose:third", "dispose:second", "dispose:first"], events.TakeLast(3));
        Assert.Collection(aggregate.InnerExceptions,
            exception => Assert.Same(secondFailure, exception),
            exception => Assert.Same(firstFailure, exception));
        Assert.Equal(1, first.Lease!.DisposeCount);
        Assert.Equal(1, second.Lease!.DisposeCount);
        Assert.Equal(1, third.Lease!.DisposeCount);
        Assert.Equal(2, registry.RetainedBuildLeaseCount);
    }

    [Fact]
    public async Task LifecycleNotificationFailureRetainsLeaseWithoutRetainingShellOrProvider()
    {
        var participant = new WeakRetentionParticipant();
        var primaryFailure = new ApplicationException("disposed observer failed");
        await using var host = BuildHost(participant, cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("lifecycle-retention", shell => shell.WithFeature<LifecycleRetentionFeature>()), services =>
            services.AddSingleton<IShellLifecycleSubscriber>(new FailFirstDisposedSubscriber(primaryFailure)));
        var registry = Assert.IsType<ShellRegistry>(host.GetRequiredService<IShellRegistry>());

        var targets = await ActivateAndFailDrainAsync(registry, participant, "lifecycle-retention");

        Assert.IsType<ShellGenerationActivationException>(targets.Failure);
        Assert.Same(primaryFailure, targets.Failure!.InnerException);
        Assert.Equal(1, registry.RetainedBuildLeaseCount);
        Assert.Equal(0, GetDisposeAttempts(targets.Lease));
        Assert.Equal(0, GetProbeDisposeCount(targets.Probe));

        var replacement = await registry.ActivateAsync("lifecycle-retention");
        Assert.Equal(2, replacement.Descriptor.Generation);
        Assert.Same(replacement, registry.GetActive("lifecycle-retention"));
        var replacementDrain = await registry.DrainAsync(replacement);
        await replacementDrain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        AssertCollectedShellAndProvider(targets);
        Assert.True(IsAlive(targets.Lease));
        Assert.Equal(1, registry.RetainedBuildLeaseCount);
    }

    [Fact]
    public async Task ProviderDisposalFailureRetainsLeaseAfterRemovingShellFromRegistry()
    {
        var participant = new WeakRetentionParticipant();
        await using var host = BuildHost(participant, cshells => cshells
            .WithAssemblyContaining<ShellGenerationBuildLeaseTests>()
            .AddShell("provider-retention", shell => shell.WithFeature<ProviderFailureRetentionFeature>()));
        var registry = Assert.IsType<ShellRegistry>(host.GetRequiredService<IShellRegistry>());

        var targets = await ActivateAndFailDrainAsync(registry, participant, "provider-retention");

        Assert.IsType<ApplicationException>(targets.Failure);
        Assert.Null(registry.GetActive("provider-retention"));
        Assert.Empty(registry.GetAll("provider-retention"));
        Assert.Equal(1, registry.RetainedBuildLeaseCount);
        Assert.Equal(0, GetDisposeAttempts(targets.Lease));
        Assert.Equal(1, GetProbeDisposeCount(targets.Probe));

        AssertCollectedShellAndProvider(targets);
        Assert.True(IsAlive(targets.Lease));
        Assert.Equal(1, registry.RetainedBuildLeaseCount);
    }

    private static ShellTestHost BuildHost(
        IShellGenerationBuildParticipant participant,
        Action<CShellsBuilder> configure,
        Action<IServiceCollection>? configureServices = null) => BuildHost([participant], configure, configureServices);

    private static ShellTestHost BuildHost(
        IReadOnlyList<IShellGenerationBuildParticipant> participants,
        Action<CShellsBuilder> configure,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        configureServices?.Invoke(services);
        foreach (var participant in participants)
            services.AddSingleton<IShellGenerationBuildParticipant>(participant);
        services.AddCShells(configure);
        return new ShellTestHost(services.BuildServiceProvider());
    }

    private sealed class ShellTestHost(ServiceProvider provider) : IServiceProvider, IAsyncDisposable
    {
        private int _disposed;

        public object? GetService(Type serviceType) => provider.GetService(serviceType);

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try
            {
                if (provider.GetService(typeof(IShellRegistry)) is IShellRegistry registry)
                {
                    foreach (var shell in registry.GetActiveShells())
                    {
                        try
                        {
                            var drain = await registry.DrainAsync(shell).ConfigureAwait(false);
                            await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        }
                        catch
                        {
                            // Preserve the test assertion while still attempting to tear down peers.
                        }
                    }
                }
            }
            finally
            {
                await provider.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<RetentionTargets> ActivateAndFailDrainAsync(
        IShellRegistry registry,
        WeakRetentionParticipant participant,
        string name)
    {
        var shell = await registry.ActivateAsync(name);
        var provider = shell.ServiceProvider;
        var probe = provider.GetRequiredService<RetentionProviderProbe>();
        var lease = participant.LastLease!;
        var shellReference = new WeakReference<IShell>(shell);
        var providerReference = new WeakReference<IServiceProvider>(provider);
        var probeReference = new WeakReference<RetentionProviderProbe>(probe);
        var drain = await registry.DrainAsync(shell);
        Exception? failure = null;
        try
        {
            await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        return new RetentionTargets(shellReference, providerReference, lease, probeReference, failure);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssertCollectedShellAndProvider(RetentionTargets targets)
    {
        for (var attempt = 0; attempt < 8 && (IsAlive(targets.Shell) || IsAlive(targets.Provider)); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Thread.Sleep(10);
        }

        Assert.False(IsAlive(targets.Shell), "The root retention collection must not keep the shell alive.");
        Assert.False(IsAlive(targets.Provider), "The root retention collection must not keep the shell provider alive.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsAlive<T>(WeakReference<T> reference) where T : class => reference.TryGetTarget(out _);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetDisposeAttempts(WeakReference<WeakRetentionLease> reference) =>
        reference.TryGetTarget(out var lease) ? lease.DisposeAttempts : -1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetProbeDisposeCount(WeakReference<RetentionProviderProbe> reference) =>
        reference.TryGetTarget(out var probe) ? probe.DisposeCount : -1;

    private sealed record RetentionTargets(
        WeakReference<IShell> Shell,
        WeakReference<IServiceProvider> Provider,
        WeakReference<WeakRetentionLease> Lease,
        WeakReference<RetentionProviderProbe> Probe,
        Exception? Failure);

    private sealed class RecordingParticipant(
        List<string> events,
        string name = "participant",
        Exception? callbackFailure = null,
        Exception? releaseFailure = null,
        Action? onSnapshot = null) : IShellGenerationBuildParticipant
    {
        public ShellGenerationBuildContext? Context { get; private set; }
        public RecordingLease? Lease { get; private set; }
        public RuntimeFeatureCatalogSnapshot? Snapshot { get; private set; }

        public Exception? CallbackFailure => callbackFailure;

        public ValueTask<IShellGenerationBuildLease> BeginAsync(
            ShellGenerationBuildContext context,
            CancellationToken cancellationToken = default)
        {
            Context = context;
            events.Add($"begin:{name}:{context.Descriptor.Generation}");
            Lease = new RecordingLease(events, name, callbackFailure, releaseFailure, onSnapshot, snapshot => Snapshot = snapshot);
            return ValueTask.FromResult<IShellGenerationBuildLease>(Lease);
        }
    }

    private sealed class MetadataBlueprint : IShellBlueprint
    {
        public string Name => "metadata-case";
        public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>
        {
            ["Foo"] = "upper",
            ["foo"] = "lower"
        };

        public Task<ShellSettings> ComposeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ShellSettings(new ShellId(Name)));
    }

    private sealed class MutatingMetadataBlueprint : IShellBlueprint
    {
        private readonly Dictionary<string, string> _metadata = new() { ["revision"] = "before" };

        public string Name => "metadata-mutated";
        public IReadOnlyDictionary<string, string> Metadata => _metadata;

        public Task<ShellSettings> ComposeAsync(CancellationToken cancellationToken = default)
        {
            _metadata["revision"] = "after";
            return Task.FromResult(new ShellSettings(new ShellId(Name)));
        }
    }

    private sealed class ThrowingBeginParticipant(Exception failure) : IShellGenerationBuildParticipant
    {
        public ValueTask<IShellGenerationBuildLease> BeginAsync(
            ShellGenerationBuildContext context,
            CancellationToken cancellationToken = default) => ValueTask.FromException<IShellGenerationBuildLease>(failure);
    }

    private sealed class ThrowingAssemblyProvider(Exception failure) : IFeatureAssemblyProvider
    {
        public Task<IEnumerable<System.Reflection.Assembly>> GetAssembliesAsync(
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken = default) => Task.FromException<IEnumerable<System.Reflection.Assembly>>(failure);
    }

    private sealed class RecordingAssemblyProvider(System.Reflection.Assembly assembly, List<string> events) : IFeatureAssemblyProvider
    {
        public Task<IEnumerable<System.Reflection.Assembly>> GetAssembliesAsync(
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken = default)
        {
            events.Add("catalog");
            return Task.FromResult<IEnumerable<System.Reflection.Assembly>>([assembly]);
        }
    }

    private sealed class CatalogPinParticipant : IShellGenerationBuildParticipant
    {
        private readonly ConcurrentDictionary<long, int> _pinCounts = new();
        private readonly ConcurrentDictionary<int, long> _selectedGenerations = new();

        public ValueTask<IShellGenerationBuildLease> BeginAsync(
            ShellGenerationBuildContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IShellGenerationBuildLease>(new CatalogPinLease(this, context));

        public int PinCount(long generation) => _pinCounts.GetValueOrDefault(generation);

        public long SelectedGeneration(int shellGeneration) => _selectedGenerations[shellGeneration];

        private sealed class CatalogPinLease(CatalogPinParticipant owner, ShellGenerationBuildContext context) : IShellGenerationBuildLease
        {
            private long? _snapshotGeneration;

            public async ValueTask OnSnapshotSelectedAsync(
                RuntimeFeatureCatalogSnapshot snapshot,
                CancellationToken cancellationToken = default)
            {
                _snapshotGeneration = snapshot.Generation;
                owner._selectedGenerations[context.Descriptor.Generation] = snapshot.Generation;
                owner._pinCounts.AddOrUpdate(snapshot.Generation, 1, static (_, count) => count + 1);
                if (context.Descriptor.Name == "pin-main" && context.Descriptor.Generation == 2)
                {
                    CatalogSnapshotGate.Entered.TrySetResult();
                    await CatalogSnapshotGate.Allow.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

            }

            public ValueTask DisposeAsync()
            {
                if (_snapshotGeneration is { } generation)
                    owner._pinCounts.AddOrUpdate(generation, 0, static (_, count) => count - 1);
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class WeakRetentionParticipant : IShellGenerationBuildParticipant
    {
        public WeakReference<WeakRetentionLease>? LastLease { get; private set; }

        public ValueTask<IShellGenerationBuildLease> BeginAsync(
            ShellGenerationBuildContext context,
            CancellationToken cancellationToken = default)
        {
            var lease = new WeakRetentionLease();
            LastLease = new WeakReference<WeakRetentionLease>(lease);
            return ValueTask.FromResult<IShellGenerationBuildLease>(lease);
        }
    }

    private sealed class BeginBarrierParticipant : IShellGenerationBuildParticipant
    {
        private int _beginCount;

        public TaskCompletionSource BothEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Allow { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int BeginCount => Volatile.Read(ref _beginCount);

        public async ValueTask<IShellGenerationBuildLease> BeginAsync(
            ShellGenerationBuildContext context,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _beginCount) == 2)
                BothEntered.TrySetResult();
            await Allow.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new NoopLease();
        }
    }

    private sealed class GenerationLeaseParticipant : IShellGenerationBuildParticipant
    {
        public ConcurrentDictionary<int, CountingLease> Leases { get; } = new();

        public ValueTask<IShellGenerationBuildLease> BeginAsync(
            ShellGenerationBuildContext context,
            CancellationToken cancellationToken = default)
        {
            var lease = new CountingLease();
            Leases[context.Descriptor.Generation] = lease;
            return ValueTask.FromResult<IShellGenerationBuildLease>(lease);
        }
    }

    private sealed class CountingLease : IShellGenerationBuildLease
    {
        public int DisposeCount { get; private set; }

        public ValueTask OnSnapshotSelectedAsync(
            RuntimeFeatureCatalogSnapshot snapshot,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoopLease : IShellGenerationBuildLease
    {
        public ValueTask OnSnapshotSelectedAsync(
            RuntimeFeatureCatalogSnapshot snapshot,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class WeakRetentionLease : IShellGenerationBuildLease
    {
        public int DisposeAttempts { get; private set; }

        public ValueTask OnSnapshotSelectedAsync(
            RuntimeFeatureCatalogSnapshot snapshot,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            DisposeAttempts++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class InvalidCompositionBlueprint(
        string name,
        CancellationTokenSource? cancellation = null,
        bool throwOnCompose = false) : IShellBlueprint
    {
        public string Name { get; } = name;
        public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

        public Task<ShellSettings> ComposeAsync(CancellationToken cancellationToken = default)
        {
            if (throwOnCompose)
                throw new ApplicationException("compose failed");
            cancellation?.Cancel();
            return Task.FromResult(new ShellSettings(new ShellId(cancellation is null ? "another-name" : Name)));
        }
    }

    private sealed class RecordingLease(
        List<string> events,
        string name,
        Exception? callbackFailure,
        Exception? releaseFailure,
        Action? onSnapshot,
        Action<RuntimeFeatureCatalogSnapshot> captureSnapshot) : IShellGenerationBuildLease
    {
        public RuntimeFeatureCatalogSnapshot? Snapshot { get; private set; }
        public Exception? CallbackFailure => callbackFailure;
        public int DisposeCount { get; private set; }

        public ValueTask OnSnapshotSelectedAsync(
            RuntimeFeatureCatalogSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            Snapshot = snapshot;
            captureSnapshot(snapshot);
            events.Add($"snapshot:{name}:{snapshot.Generation}");
            onSnapshot?.Invoke();
            return callbackFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(callbackFailure);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            events.Add($"dispose:{name}");
            return releaseFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(releaseFailure);
        }
    }

    [ShellFeature("BuildLeaseOrder")]
    public sealed class LeaseOrderFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) => BuildLeaseTestEvents.Events?.Add("feature");
    }

    private static class BuildLeaseTestEvents
    {
        public static List<string>? Events { get; set; }
    }

    [ShellFeature("BuildLeaseBlockingDisposal")]
    public sealed class BlockingDisposalFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) => services.AddSingleton<BlockingShellDisposal>();
    }

    [ShellFeature("BuildLeaseBlockingCandidateInitializer")]
    public sealed class BlockingCandidateFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) => services.AddTransient<IShellInitializer, BlockingCandidateInitializer>();
    }

    public sealed class BlockingCandidateInitializer(IShell shell) : IShellInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (shell.Descriptor.Name.Equals("pin-main", StringComparison.OrdinalIgnoreCase)
                && shell.Descriptor.Generation == 2)
            {
                CandidateInitializerGate.InitializerCalls++;
                CandidateInitializerGate.PinCountAtInitializer = CandidateInitializerGate.ReadPinCount?.Invoke() ?? -1;
            }

            return Task.CompletedTask;
        }
    }

    private static class CatalogSnapshotGate
    {
        public static TaskCompletionSource Entered { get; private set; } = NewSource();
        public static TaskCompletionSource Allow { get; private set; } = NewSource();

        public static void Reset()
        {
            Entered = NewSource();
            Allow = NewSource();
        }

        private static TaskCompletionSource NewSource() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static class CandidateInitializerGate
    {
        public static int InitializerCalls { get; set; }
        public static int PinCountAtInitializer { get; set; }
        public static Func<int>? ReadPinCount { get; set; }

        public static void Reset()
        {
            InitializerCalls = 0;
            PinCountAtInitializer = 0;
            ReadPinCount = null;
        }
    }

    public sealed class BlockingShellDisposal : IAsyncDisposable
    {
        public TaskCompletionSource DisposeEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowDispose { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask DisposeAsync()
        {
            DisposeEntered.TrySetResult();
            await AllowDispose.Task.ConfigureAwait(false);
        }
    }

    [ShellFeature("BuildLeaseLifecycleRetention")]
    public sealed class LifecycleRetentionFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) => services.AddSingleton<RetentionProviderProbe>();
    }

    [ShellFeature("BuildLeaseProviderFailureRetention")]
    public sealed class ProviderFailureRetentionFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) => services.AddSingleton(_ => new RetentionProviderProbe(throwOnDispose: true));
    }

    [ShellFeature("BuildLeaseActivationRollbackDisposeFailure")]
    public sealed class ActivationRollbackDisposeFailureFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) => services.AddSingleton(_ => new RetentionProviderProbe(throwOnDispose: true));
    }

    public sealed class RetentionProviderProbe(bool throwOnDispose = false) : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return throwOnDispose
                ? ValueTask.FromException(new ApplicationException("provider dispose failed"))
                : ValueTask.CompletedTask;
        }
    }

    [ShellFeature("BuildLeaseInitializerFailure")]
    public sealed class InitializerFailureFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<RetentionProviderProbe>();
            services.AddTransient<IShellInitializer, ThrowingTestInitializer>();
        }
    }

    [ShellFeature("BuildLeaseInitializerDisposeFailure")]
    public sealed class InitializerFailureDisposeFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(_ => new RetentionProviderProbe(throwOnDispose: true));
            services.AddTransient<IShellInitializer, ThrowingTestInitializer>();
        }
    }

    public sealed class ThrowingTestInitializer(RetentionProviderProbe probe) : IShellInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _ = probe;
            throw new ApplicationException("initializer failed");
        }
    }

    private sealed class RecordingLifecycleSubscriber : IShellLifecycleSubscriber
    {
        public List<(ShellLifecycleState Previous, ShellLifecycleState Current)> Transitions { get; } = [];

        public Task OnStateChangedAsync(
            IShell shell,
            ShellLifecycleState previous,
            ShellLifecycleState current,
            CancellationToken cancellationToken = default)
        {
            Transitions.Add((previous, current));
            return Task.CompletedTask;
        }
    }

    private sealed class FailFirstDisposedSubscriber(Exception failure) : IShellLifecycleSubscriber
    {
        private int _failed;

        public Task OnStateChangedAsync(
            IShell shell,
            ShellLifecycleState previous,
            ShellLifecycleState current,
            CancellationToken cancellationToken = default) =>
            current == ShellLifecycleState.Disposed && Interlocked.Exchange(ref _failed, 1) == 0
                ? throw new ShellGenerationActivationException(shell.Descriptor, failure)
                : Task.CompletedTask;
    }

    [ShellFeature("BuildLeaseThrowsDuringConstruction")]
    public sealed class ThrowingFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) => throw new ApplicationException("feature construction failed");
    }

    private sealed class ThrowingActivationParticipant(Exception commitFailure, bool failProviderDispose = false) : IShellGenerationActivationParticipant
    {
        public Task PrepareAsync(IShell shell, CancellationToken cancellationToken = default)
        {
            if (failProviderDispose)
                _ = shell.ServiceProvider.GetRequiredService<RetentionProviderProbe>();
            return Task.CompletedTask;
        }
        public void Commit(IShell shell) => throw commitFailure;
        public void Complete(IShell shell) { }
        public void Rollback(IShell shell) => throw new ApplicationException("rollback logging should be safe");
    }

    private sealed class ThrowingErrorLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= Microsoft.Extensions.Logging.LogLevel.Error)
                throw new ApplicationException("logger failed");
        }
    }
}
