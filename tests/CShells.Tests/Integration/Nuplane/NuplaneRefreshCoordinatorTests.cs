using CShells.Lifecycle;
using CShells.Nuplane;
using CShells.Nuplane.Internal;
using Microsoft.Extensions.DependencyInjection;
using Nuplane.Abstractions;
using Nuplane.Loading;

namespace CShells.Tests.Integration.Nuplane;

public sealed class NuplaneRefreshCoordinatorTests
{
    [Fact]
    public async Task ColdChangedWorkIsDeferredUntilBuildAndUnchangedCallbackDoesNotReloadFirstShell()
    {
        var packageCatalog = new FakePackageAssemblyCatalog
        {
            Packages = [FakePackageAssemblyCatalog.CreatePackage("features", typeof(NuplaneCompositionTests.DiscoveredPackageFeature).Assembly)]
        };
        await using var host = NuplaneTestHost.Build(
            services => services.AddSingleton<IPackageAssemblyCatalog>(packageCatalog),
            shells => shells.WithNuplaneFeatureDiscovery(options => options.AutoReload = true)
                .AddShell("cold", shell => shell.WithFeature("NuplaneDiscovered")));
        var observer = host.GetRequiredService<INuplaneObserver>();

        await NuplaneCoordinatorTestCases.NotifyAsync(observer);
        Assert.Equal(0, packageCatalog.QueryCount);
        Assert.Empty(host.GetRequiredService<IShellRegistry>().GetActiveShells());

        var registry = host.GetRequiredService<IShellRegistry>();
        var firstShell = await registry.ActivateAsync("cold");
        Assert.Equal(1, packageCatalog.QueryCount);
        Assert.NotNull(firstShell.ServiceProvider.GetService<NuplaneCompositionTests.DiscoveredFeatureMarker>());

        await NuplaneCoordinatorTestCases.NotifyAsync(observer, sourceChanged: false);

        Assert.Equal(1, packageCatalog.QueryCount);
        Assert.Same(firstShell, registry.GetActive("cold"));
    }

    [Fact]
    public async Task LastCommittedRemovalRefreshesTheCatalogToZeroFeatures()
    {
        var packageCatalog = new FakePackageAssemblyCatalog
        {
            Packages = [FakePackageAssemblyCatalog.CreatePackage("features", typeof(NuplaneCompositionTests.DiscoveredPackageFeature).Assembly)]
        };
        await using var host = NuplaneTestHost.Build(
            services => services.AddSingleton<IPackageAssemblyCatalog>(packageCatalog),
            shells => shells.WithNuplaneFeatureDiscovery()
                .AddShell("remove-last", shell => shell.WithFeature("NuplaneDiscovered")));
        var registry = host.GetRequiredService<IShellRegistry>();
        await registry.ActivateAsync("remove-last");
        var observer = host.GetRequiredService<INuplaneObserver>();
        var removal = FakePackageAssemblyCatalog.ChangeSet(removed: ["features"]);
        packageCatalog.Packages = [];

        await observer.OnPackagesReconciledAsync(removal, [], CancellationToken.None);

        Assert.Equal(2, packageCatalog.QueryCount);
        Assert.DoesNotContain(
            (await host.GetRequiredService<CShells.Features.IRuntimeFeatureCatalog>().GetSnapshotAsync()).FeatureDescriptors,
            feature => feature.Id == "NuplaneDiscovered");
    }

    [Fact]
    public async Task ColdRemovalDoesNotScanUntilBuildAndRefreshesToZeroBeforeFeatureSelection()
    {
        var packageCatalog = new FakePackageAssemblyCatalog
        {
            Packages = [FakePackageAssemblyCatalog.CreatePackage("features", typeof(NuplaneCompositionTests.DiscoveredPackageFeature).Assembly)]
        };
        await using var host = NuplaneTestHost.Build(
            services => services.AddSingleton<IPackageAssemblyCatalog>(packageCatalog),
            shells => shells.WithNuplaneFeatureDiscovery()
                .AddShell("removed-cold-feature", shell => shell.WithFeature("NuplaneDiscovered")));
        var runtimeCatalog = host.GetRequiredService<CShells.Features.IRuntimeFeatureCatalog>();
        Assert.Contains("NuplaneDiscovered", (await runtimeCatalog.GetSnapshotAsync()).FeatureDescriptors.Select(feature => feature.Id));
        packageCatalog.Packages = [];
        var removal = FakePackageAssemblyCatalog.ChangeSet(removed: ["features"]);

        await host.GetRequiredService<INuplaneObserver>().OnPackagesReconciledAsync(removal, [], CancellationToken.None);

        Assert.Equal(1, packageCatalog.QueryCount);
        Assert.Empty(host.GetRequiredService<IShellRegistry>().GetActiveShells());
        var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync("removed-cold-feature");

        Assert.Equal(2, packageCatalog.QueryCount);
        Assert.DoesNotContain(runtimeCatalog.CurrentSnapshot.FeatureDescriptors, feature => feature.Id == "NuplaneDiscovered");
        Assert.Null(shell.ServiceProvider.GetService<NuplaneCompositionTests.DiscoveredFeatureMarker>());
    }

    [Fact]
    public async Task DisabledObserverDoesNotPreventBuildFromConsumingPreviouslyPendingFreshness()
    {
        var options = new NuplaneIntegrationOptions();
        var catalog = new TestRuntimeFeatureCatalog();
        var registry = TestShellRegistry.Create(out var registryState);
        registryState.HasActiveShell = false;
        var coordinator = new NuplaneRefreshCoordinator(catalog, new TestOptionsMonitor<NuplaneIntegrationOptions>(options), () => registry);

        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator);
        options.Enabled = false;
        var context = new ShellGenerationBuildContext(
            ShellDescriptor.Create("deferred", 1),
            new ShellId("deferred"));

        await coordinator.BeginAsync(context);

        Assert.Equal(1, catalog.RefreshCount);
    }

    [Fact]
    public async Task FailedOnlyCompletionDoesNotRequestRefreshOrInspectRegistry()
    {
        var (coordinator, catalog, registryState) = CreateCoordinator();
        var changeSet = FakePackageAssemblyCatalog.ChangeSet(added: [FakePackageAssemblyCatalog.Resolved("not-applied")]);

        await coordinator.OnPackagesReconciledAsync(changeSet, [], CancellationToken.None);

        Assert.Equal(0, catalog.RefreshCount);
        Assert.Equal(0, registryState.ReloadCount);
        Assert.Equal(0, registryState.ActiveShellReadCount);
    }

    [Fact]
    public async Task NewerEventDuringRefreshRemainsPendingAndIsRefreshedAfterGateOpens()
    {
        var firstRefreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondActiveRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = new TestRuntimeFeatureCatalog
        {
            RefreshHandler = async (number, _) =>
            {
                if (number != 1)
                    return;

                firstRefreshStarted.TrySetResult();
                await releaseFirstRefresh.Task;
            }
        };
        var registry = TestShellRegistry.Create(out var registryState);
        registryState.ActiveShellsRead = count =>
        {
            if (count == 2)
                secondActiveRead.TrySetResult();
        };
        var coordinator = new NuplaneRefreshCoordinator(catalog, new TestOptionsMonitor<NuplaneIntegrationOptions>(new NuplaneIntegrationOptions()), () => registry);

        Task? first = null;
        Task? second = null;
        await NuplaneCoordinatorTestCases.RunWithGateCleanupAsync(
            async () =>
            {
                first = NuplaneCoordinatorTestCases.NotifyAsync(coordinator);
                await firstRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                second = NuplaneCoordinatorTestCases.NotifyAsync(coordinator);
                await secondActiveRead.Task.WaitAsync(TimeSpan.FromSeconds(5));
                releaseFirstRefresh.TrySetResult();
                await Task.WhenAll(first!, second!).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(2, catalog.RefreshCount);
            },
            () => releaseFirstRefresh.TrySetResult(),
            () => first,
            () => second);
    }

    [Fact]
    public async Task FailedRefreshRetainsFreshnessForAnUnchangedEligibleRetry()
    {
        var catalog = new TestRuntimeFeatureCatalog
        {
            RefreshHandler = (number, _) => number == 1
                ? Task.FromException(new InvalidOperationException("refresh failed"))
                : Task.CompletedTask
        };
        var registry = TestShellRegistry.Create(out _);
        var coordinator = new NuplaneRefreshCoordinator(catalog, new TestOptionsMonitor<NuplaneIntegrationOptions>(new NuplaneIntegrationOptions()), () => registry);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => NuplaneCoordinatorTestCases.NotifyAsync(coordinator));
        Assert.Equal("refresh failed", exception.Message);

        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator, sourceChanged: false);

        Assert.Equal(2, catalog.RefreshCount);
    }

    [Fact]
    public async Task FailedBuildRefreshRetainsFreshnessForTheNextBuildAttempt()
    {
        var catalog = new TestRuntimeFeatureCatalog
        {
            RefreshHandler = (number, _) => number == 1
                ? Task.FromException(new InvalidOperationException("first build refresh failed"))
                : Task.CompletedTask
        };
        var registry = TestShellRegistry.Create(out var registryState);
        registryState.HasActiveShell = false;
        var coordinator = new NuplaneRefreshCoordinator(catalog, new TestOptionsMonitor<NuplaneIntegrationOptions>(new NuplaneIntegrationOptions()), () => registry);
        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator);
        var context = new ShellGenerationBuildContext(ShellDescriptor.Create("retry-build", 1), new ShellId("retry-build"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.BeginAsync(context).AsTask());
        await coordinator.BeginAsync(context);

        Assert.Equal(2, catalog.RefreshCount);
    }

    [Fact]
    public async Task CancellationAfterRefreshReturnsDoesNotAcknowledgeCatalogEpoch()
    {
        using var cancellation = new CancellationTokenSource();
        var catalog = new TestRuntimeFeatureCatalog
        {
            RefreshHandler = (number, _) =>
            {
                if (number == 1)
                    cancellation.Cancel();
                return Task.CompletedTask;
            }
        };
        var registry = TestShellRegistry.Create(out _);
        var coordinator = new NuplaneRefreshCoordinator(catalog, new TestOptionsMonitor<NuplaneIntegrationOptions>(new NuplaneIntegrationOptions()), () => registry);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NuplaneCoordinatorTestCases.NotifyAsync(coordinator, cancellationToken: cancellation.Token));
        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator, sourceChanged: false);

        Assert.Equal(2, catalog.RefreshCount);
    }

    [Fact]
    public async Task GenericDefaultsAndFoundationAndWorkbenchPolicyProfilesRemainIndependent()
    {
        var defaults = new NuplaneIntegrationOptions();
        Assert.True(defaults.Enabled);
        Assert.Equal(NuplaneRefreshTrigger.ChangedOrPending, defaults.RefreshTrigger);
        Assert.False(defaults.AutoReload);

        var foundationProfile = new NuplaneIntegrationOptions
        {
            RefreshTrigger = NuplaneRefreshTrigger.EveryEligibleCompletion,
            AutoReload = true
        };
        var (foundation, foundationCatalog, foundationRegistry) = CreateCoordinator(foundationProfile);
        await NuplaneCoordinatorTestCases.NotifyAsync(foundation, sourceChanged: false);
        await NuplaneCoordinatorTestCases.NotifyAsync(foundation, sourceChanged: false);
        Assert.Equal(2, foundationCatalog.RefreshCount);
        Assert.Equal(2, foundationRegistry.ReloadCount);

        var workbenchProfile = new NuplaneIntegrationOptions
        {
            RefreshTrigger = NuplaneRefreshTrigger.ChangedOrPending,
            AutoReload = false
        };
        var (workbench, workbenchCatalog, workbenchRegistry) = CreateCoordinator(workbenchProfile);
        await NuplaneCoordinatorTestCases.NotifyAsync(workbench);
        await NuplaneCoordinatorTestCases.NotifyAsync(workbench, sourceChanged: false);
        Assert.Equal(1, workbenchCatalog.RefreshCount);
        Assert.Equal(0, workbenchRegistry.ReloadCount);
    }

    [Fact]
    public async Task NewSourceEpochArrivingDuringAutomaticReloadIsNotAcknowledgedByTheEarlierReload()
    {
        var reloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstReload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRefreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = new TestRuntimeFeatureCatalog
        {
            RefreshHandler = (number, _) =>
            {
                if (number == 2)
                    secondRefreshStarted.TrySetResult();
                return Task.CompletedTask;
            }
        };
        var registry = TestShellRegistry.Create(out var registryState);
        registryState.ReloadHandler = async (number, _) =>
        {
            if (number == 1)
            {
                reloadStarted.TrySetResult();
                await releaseFirstReload.Task;
            }

            return [new ReloadResult("test", null, null, null)];
        };
        var coordinator = new NuplaneRefreshCoordinator(
            catalog,
            new TestOptionsMonitor<NuplaneIntegrationOptions>(new NuplaneIntegrationOptions { AutoReload = true }),
            () => registry);

        Task? first = null;
        Task? second = null;
        await NuplaneCoordinatorTestCases.RunWithGateCleanupAsync(
            async () =>
            {
                first = NuplaneCoordinatorTestCases.NotifyAsync(coordinator);
                await reloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                second = NuplaneCoordinatorTestCases.NotifyAsync(coordinator);
                await secondRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                releaseFirstReload.TrySetResult();
                await Task.WhenAll(first!, second!).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(2, catalog.RefreshCount);
                Assert.Equal(2, registryState.ReloadCount);
            },
            () => releaseFirstReload.TrySetResult(),
            () => first,
            () => second);
    }

    private static (NuplaneRefreshCoordinator Coordinator, TestRuntimeFeatureCatalog Catalog, TestShellRegistry Registry)
        CreateCoordinator(NuplaneIntegrationOptions? options = null)
    {
        var catalog = new TestRuntimeFeatureCatalog();
        var registry = TestShellRegistry.Create(out var registryState);
        var coordinator = new NuplaneRefreshCoordinator(catalog, new TestOptionsMonitor<NuplaneIntegrationOptions>(options ?? new NuplaneIntegrationOptions()), () => registry);
        return (coordinator, catalog, registryState);
    }

}
