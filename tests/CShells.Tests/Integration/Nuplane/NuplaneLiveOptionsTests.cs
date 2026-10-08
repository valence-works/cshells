using CShells.Lifecycle;
using CShells.Nuplane;
using CShells.Nuplane.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nuplane.Abstractions;
using Nuplane.Loading;

namespace CShells.Tests.Integration.Nuplane;

public sealed class NuplaneLiveOptionsTests
{
    [Fact(DisplayName = "An eligible delivery uses its captured options while later deliveries see replacements")]
    public async Task DeliveryPolicy_CapturedBeforeGatedRefresh_RemainsStableForThatDelivery()
    {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldCallbackCount = 0;
        var newCallbackCount = 0;
        var original = new NuplaneIntegrationOptions
        {
            RefreshTrigger = NuplaneRefreshTrigger.EveryEligibleCompletion,
            AutoReload = true,
            OnReloadResults = (_, _) =>
            {
                Interlocked.Increment(ref oldCallbackCount);
                return ValueTask.CompletedTask;
            }
        };
        var monitor = new TestOptionsMonitor<NuplaneIntegrationOptions>(original);
        var catalog = new TestRuntimeFeatureCatalog
        {
            RefreshHandler = async (number, _) =>
            {
                if (number != 1)
                    return;

                refreshStarted.TrySetResult();
                await releaseRefresh.Task;
            }
        };
        var registry = TestShellRegistry.Create(out var registryState);
        var coordinator = new NuplaneRefreshCoordinator(catalog, monitor, () => registry);
        Task? firstDelivery = null;

        await NuplaneCoordinatorTestCases.RunWithGateCleanupAsync(
            async () =>
            {
                firstDelivery = NuplaneCoordinatorTestCases.NotifyAsync(coordinator);
                await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

                original.Enabled = false;
                original.RefreshTrigger = (NuplaneRefreshTrigger)99;
                original.AutoReload = false;
                original.OnReloadResults = (_, _) => ValueTask.FromException(new InvalidOperationException("The mutated callback must not run."));
                monitor.Replace(new NuplaneIntegrationOptions
                {
                    RefreshTrigger = NuplaneRefreshTrigger.ChangedOrPending,
                    AutoReload = false,
                    OnReloadResults = (_, _) =>
                    {
                        Interlocked.Increment(ref newCallbackCount);
                        return ValueTask.CompletedTask;
                    }
                });

                releaseRefresh.TrySetResult();
                await firstDelivery.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Equal(1, catalog.RefreshCount);
                Assert.Equal(1, registryState.ReloadCount);
                Assert.Equal(1, oldCallbackCount);
                Assert.Equal(0, newCallbackCount);

                await NuplaneCoordinatorTestCases.NotifyAsync(coordinator, sourceChanged: false);
                Assert.Equal(1, catalog.RefreshCount);
                Assert.Equal(1, registryState.ReloadCount);
                Assert.Equal(0, newCallbackCount);

                monitor.Replace(new NuplaneIntegrationOptions
                {
                    RefreshTrigger = NuplaneRefreshTrigger.ChangedOrPending,
                    AutoReload = true,
                    OnReloadResults = (_, _) =>
                    {
                        Interlocked.Increment(ref newCallbackCount);
                        return ValueTask.CompletedTask;
                    }
                });
                await NuplaneCoordinatorTestCases.NotifyAsync(coordinator);

                Assert.Equal(2, catalog.RefreshCount);
                Assert.Equal(2, registryState.ReloadCount);
                Assert.Equal(1, newCallbackCount);
            },
            () => releaseRefresh.TrySetResult(),
            () => firstDelivery);
    }

    [Fact(DisplayName = "Disabled deliveries preserve pending build freshness and add no new epochs")]
    public async Task DisabledDelivery_PendingBuildFreshness_IsPreservedWithoutNewEpochs()
    {
        var monitor = new TestOptionsMonitor<NuplaneIntegrationOptions>(new NuplaneIntegrationOptions());
        var catalog = new TestRuntimeFeatureCatalog();
        var registry = TestShellRegistry.Create(out var registryState);
        registryState.HasActiveShell = false;
        var coordinator = new NuplaneRefreshCoordinator(catalog, monitor, () => registry);
        var context = new ShellGenerationBuildContext(ShellDescriptor.Create("deferred", 1), new ShellId("deferred"));

        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator);
        Assert.Equal(1, monitor.ReadCount);
        monitor.Replace(new NuplaneIntegrationOptions { Enabled = false });
        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator);
        Assert.Equal(2, monitor.ReadCount);

        await using var pendingLease = await coordinator.BeginAsync(context);
        Assert.Equal(1, catalog.RefreshCount);

        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator);
        Assert.Equal(3, monitor.ReadCount);
        await using var unchangedLease = await coordinator.BeginAsync(context);

        Assert.Equal(1, catalog.RefreshCount);
        Assert.Equal(3, monitor.ReadCount);
        Assert.Equal(1, registryState.ActiveShellReadCount);
        Assert.Equal(0, registryState.ReloadCount);
    }

    [Fact(DisplayName = "Turning automatic reload off preserves an existing failed reload for a later retry")]
    public async Task AutoReload_OffAfterFailedReload_PreservesRetryWithoutRescan()
    {
        var firstReloadFailure = new InvalidOperationException("First reload did not promote the shell.");
        var monitor = new TestOptionsMonitor<NuplaneIntegrationOptions>(new NuplaneIntegrationOptions { AutoReload = true });
        var catalog = new TestRuntimeFeatureCatalog();
        var registry = TestShellRegistry.Create(out var registryState);
        registryState.ReloadHandler = (number, _) => Task.FromResult<IReadOnlyList<ReloadResult>>(
            number == 1
                ? [new ReloadResult("test", null, null, firstReloadFailure)]
                : [new ReloadResult("test", null, null, null)]);
        var coordinator = new NuplaneRefreshCoordinator(catalog, monitor, () => registry);

        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator);
        monitor.Replace(new NuplaneIntegrationOptions { AutoReload = false });
        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator, sourceChanged: false);

        Assert.Equal(1, catalog.RefreshCount);
        Assert.Equal(1, registryState.ReloadCount);

        monitor.Replace(new NuplaneIntegrationOptions { AutoReload = true });
        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator, sourceChanged: false);

        Assert.Equal(1, catalog.RefreshCount);
        Assert.Equal(2, registryState.ReloadCount);
    }

    [Fact(DisplayName = "Empty, failed-only, and pre-cancelled callbacks never read the options monitor")]
    public async Task IneligibleDelivery_EmptyFailedOnlyOrCancelled_DoesNotReadOptions()
    {
        var monitor = new TestOptionsMonitor<NuplaneIntegrationOptions>(new NuplaneIntegrationOptions());
        var catalog = new TestRuntimeFeatureCatalog();
        var registry = TestShellRegistry.Create(out var registryState);
        var coordinator = new NuplaneRefreshCoordinator(catalog, monitor, () => registry);
        var empty = FakePackageAssemblyCatalog.ChangeSet();
        var failedOnly = FakePackageAssemblyCatalog.ChangeSet(added: [FakePackageAssemblyCatalog.Resolved("failed-only")]);

        await coordinator.OnPackagesReconciledAsync(empty, [], CancellationToken.None);
        await coordinator.OnPackagesReconciledAsync(failedOnly, [], CancellationToken.None);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            NuplaneCoordinatorTestCases.NotifyAsync(coordinator, cancellationToken: canceled.Token));

        Assert.Equal(0, monitor.ReadCount);
        Assert.Equal(0, catalog.RefreshCount);
        Assert.Equal(0, registryState.ActiveShellReadCount);
        Assert.Equal(0, registryState.ReloadCount);
    }

    [Fact(DisplayName = "Options validation and invalid triggers fail before recording catalog work")]
    public async Task OptionsValidationOrInvalidTrigger_FailsBeforeCatalogWorkIsRecorded()
    {
        var monitorReadFailure = new InvalidOperationException("Options monitor failed.");
        var validationFailure = new OptionsValidationException(
            Options.DefaultName,
            typeof(NuplaneIntegrationOptions),
            ["The configured options were rejected."]);
        var monitor = new TestOptionsMonitor<NuplaneIntegrationOptions>(new NuplaneIntegrationOptions());
        var loggerProvider = new NuplaneTestLoggerProvider();
        var catalog = new TestRuntimeFeatureCatalog();
        var registry = TestShellRegistry.Create(out var registryState);
        var coordinator = new NuplaneRefreshCoordinator(catalog, monitor, () => registry, loggerProvider.CreateTypedLogger<NuplaneRefreshCoordinator>());
        monitor.ThrowOnRead(monitorReadFailure);

        var monitorThrown = await Assert.ThrowsAsync<InvalidOperationException>(() => NuplaneCoordinatorTestCases.NotifyAsync(coordinator));
        Assert.Same(monitorReadFailure, monitorThrown);
        Assert.Equal(0, catalog.RefreshCount);

        monitor.ThrowOnRead(null);
        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator, sourceChanged: false);
        Assert.Equal(0, catalog.RefreshCount);
        Assert.Equal(1, registryState.ActiveShellReadCount);
        Assert.Equal(0, registryState.ReloadCount);

        monitor.ThrowOnRead(validationFailure);
        var validationThrown = await Assert.ThrowsAsync<OptionsValidationException>(() => NuplaneCoordinatorTestCases.NotifyAsync(coordinator));
        Assert.Same(validationFailure, validationThrown);
        monitor.ThrowOnRead(null);
        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator, sourceChanged: false);
        Assert.Equal(0, catalog.RefreshCount);
        Assert.Equal(2, registryState.ActiveShellReadCount);

        monitor.Replace(new NuplaneIntegrationOptions { RefreshTrigger = (NuplaneRefreshTrigger)99 });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => NuplaneCoordinatorTestCases.NotifyAsync(coordinator));
        monitor.Replace(new NuplaneIntegrationOptions());
        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator, sourceChanged: false);

        Assert.Equal(0, catalog.RefreshCount);
        Assert.Equal(3, registryState.ActiveShellReadCount);
        Assert.Equal(0, registryState.ReloadCount);
        Assert.Empty(loggerProvider.Snapshot());
    }

    [Fact(DisplayName = "Configuration reload changes the policy used by the registered Nuplane observer")]
    public async Task ConfigurationReload_UpdatesRegisteredObserverPolicy_OnLaterDelivery()
    {
        var packageCatalog = new FakePackageAssemblyCatalog
        {
            Packages = [FakePackageAssemblyCatalog.CreatePackage("features", typeof(NuplaneCompositionTests.DiscoveredPackageFeature).Assembly)]
        };
        var data = new Dictionary<string, string?>
        {
            ["NuplaneIntegration:Enabled"] = "true",
            ["NuplaneIntegration:RefreshTrigger"] = nameof(NuplaneRefreshTrigger.ChangedOrPending),
            ["NuplaneIntegration:AutoReload"] = "false"
        };
        using var configurationRoot = (ConfigurationRoot)new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        IConfiguration configuration = configurationRoot;

        await using var host = NuplaneTestHost.Build(
            services =>
            {
                services.AddSingleton<IConfiguration>(configuration);
                services.AddOptions<NuplaneIntegrationOptions>().Bind(configuration.GetSection("NuplaneIntegration"));
                services.AddSingleton<IPackageAssemblyCatalog>(packageCatalog);
            },
            shells => shells.WithNuplaneFeatureDiscovery()
                .AddShell("live-options", shell => shell.WithFeature("NuplaneDiscovered")));

        var registry = host.GetRequiredService<IShellRegistry>();
        var initialShell = await registry.ActivateAsync("live-options");
        var observer = host.GetRequiredService<INuplaneObserver>();
        await NuplaneCoordinatorTestCases.NotifyAsync(observer);
        Assert.Same(initialShell, registry.GetActive("live-options"));

        var memoryProvider = Assert.IsType<Microsoft.Extensions.Configuration.Memory.MemoryConfigurationProvider>(configurationRoot.Providers.Single());
        memoryProvider.Set("NuplaneIntegration:AutoReload", "true");
        configurationRoot.Reload();
        await NuplaneCoordinatorTestCases.NotifyAsync(observer);

        Assert.NotSame(initialShell, registry.GetActive("live-options"));
        Assert.Equal(3, packageCatalog.QueryCount);
    }
}
