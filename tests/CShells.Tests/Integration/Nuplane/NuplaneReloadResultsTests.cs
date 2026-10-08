using CShells.Lifecycle;
using CShells.Nuplane;
using CShells.Nuplane.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nuplane.Abstractions;
using Nuplane.Loading;

namespace CShells.Tests.Integration.Nuplane;

public sealed class NuplaneReloadResultsTests
{
    [Fact]
    public async Task PartialErrorsReachCallbackUnchangedAndReloadRetriesWithoutAnotherCatalogScan()
    {
        var refusal = new InvalidOperationException("host refusal");
        var nested = new ApplicationException("shell activation failed", refusal);
        IReadOnlyList<ReloadResult>? callbackResults = null;
        var callbackCount = 0;
        var options = new NuplaneIntegrationOptions
        {
            AutoReload = true,
            OnReloadResults = (results, _) =>
            {
                callbackCount++;
                callbackResults = results;
                return ValueTask.CompletedTask;
            }
        };
        var (coordinator, catalog, registry, registryState) = CreateCoordinator(options);
        var success = new ReloadResult("successful-shell", null, null, null);
        var failed = new ReloadResult("refused-shell", null, null, nested);
        registryState.Results = [success, failed];

        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator);

        Assert.NotNull(callbackResults);
        Assert.Same(success, callbackResults[0]);
        Assert.Same(failed, callbackResults[1]);
        Assert.Same(nested, callbackResults[1].Error);
        Assert.Same(refusal, callbackResults[1].Error!.InnerException);
        var readOnlyResults = Assert.IsAssignableFrom<IList<ReloadResult>>(callbackResults);
        Assert.True(readOnlyResults.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => readOnlyResults[0] = failed);

        registryState.Results = [new ReloadResult("successful-shell", null, null, null)];
        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator, sourceChanged: false);

        Assert.Equal(1, catalog.RefreshCount);
        Assert.Equal(2, registryState.ReloadCount);
        Assert.Equal(2, callbackCount);
    }

    [Fact]
    public async Task CallbackFailureRetainsReloadWorkAndOriginalException()
    {
        var callbackFault = new InvalidOperationException("result adapter failed");
        var callbackCount = 0;
        var options = new NuplaneIntegrationOptions
        {
            AutoReload = true,
            OnReloadResults = (_, _) =>
            {
                if (Interlocked.Increment(ref callbackCount) == 1)
                    return ValueTask.FromException(callbackFault);

                return ValueTask.CompletedTask;
            }
        };
        var (coordinator, catalog, registry, registryState) = CreateCoordinator(options);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => NuplaneCoordinatorTestCases.NotifyAsync(coordinator));
        Assert.Same(callbackFault, thrown);

        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator, sourceChanged: false);

        Assert.Equal(1, catalog.RefreshCount);
        Assert.Equal(2, registryState.ReloadCount);
        Assert.Equal(2, callbackCount);
        Assert.Single(registryState.Results);
    }

    [Fact]
    public async Task ThrownRegistryFailureEscapesUnchangedAndDoesNotInvokeResultCallback()
    {
        var registryFault = new InvalidOperationException("registry call failed");
        var callbackCount = 0;
        var options = new NuplaneIntegrationOptions
        {
            AutoReload = true,
            OnReloadResults = (_, _) =>
            {
                callbackCount++;
                return ValueTask.CompletedTask;
            }
        };
        var (coordinator, catalog, registry, registryState) = CreateCoordinator(options);
        registryState.ReloadHandler = (number, _) => number == 1
            ? Task.FromException<IReadOnlyList<ReloadResult>>(registryFault)
            : Task.FromResult<IReadOnlyList<ReloadResult>>([new ReloadResult("test", null, null, null)]);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => NuplaneCoordinatorTestCases.NotifyAsync(coordinator));
        Assert.Same(registryFault, thrown);
        Assert.Equal(0, callbackCount);

        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator, sourceChanged: false);

        Assert.Equal(1, catalog.RefreshCount);
        Assert.Equal(2, registryState.ReloadCount);
        Assert.Equal(1, callbackCount);
    }

    [Fact]
    public async Task CancellationAfterResultCallbackReturnsLeavesReloadEpochPending()
    {
        using var cancellation = new CancellationTokenSource();
        var callbackCount = 0;
        var options = new NuplaneIntegrationOptions
        {
            AutoReload = true,
            OnReloadResults = (_, _) =>
            {
                if (Interlocked.Increment(ref callbackCount) == 1)
                    cancellation.Cancel();

                return ValueTask.CompletedTask;
            }
        };
        var (coordinator, catalog, _, registryState) = CreateCoordinator(options);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NuplaneCoordinatorTestCases.NotifyAsync(coordinator, cancellationToken: cancellation.Token));

        await NuplaneCoordinatorTestCases.NotifyAsync(coordinator, sourceChanged: false);

        Assert.Equal(1, catalog.RefreshCount);
        Assert.Equal(2, registryState.ReloadCount);
        Assert.Equal(2, callbackCount);
    }

    [Fact]
    public async Task ResultCallbackCanRequestManualReloadAfterAutomaticReloadReleasesRefreshGate()
    {
        var packageCatalog = new FakePackageAssemblyCatalog
        {
            Packages = [FakePackageAssemblyCatalog.CreatePackage("features", typeof(NuplaneCompositionTests.DiscoveredPackageFeature).Assembly)]
        };
        IShellRegistry? registry = null;
        var manualReloadResults = 0;
        await using var host = NuplaneTestHost.Build(
            services =>
            {
                services.AddSingleton<IPackageAssemblyCatalog>(packageCatalog);
                services.AddOptions<NuplaneIntegrationOptions>()
                    .Configure(options =>
                    {
                        options.AutoReload = true;
                        options.OnReloadResults = async (_, cancellationToken) =>
                        {
                            var results = await registry!.ReloadActiveAsync(cancellationToken: cancellationToken);
                            Assert.All(results, result => Assert.Null(result.Error));
                            Interlocked.Increment(ref manualReloadResults);
                        };
                    });
            },
            shells => shells.WithNuplaneFeatureDiscovery()
                .AddShell("reentrant-reload", shell => shell.WithFeature("NuplaneDiscovered")));
        registry = host.GetRequiredService<IShellRegistry>();
        await registry.ActivateAsync("reentrant-reload");
        var observer = host.GetRequiredService<INuplaneObserver>();

        await NuplaneCoordinatorTestCases.NotifyAsync(observer).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, manualReloadResults);
        Assert.Equal(3, registry.GetActive("reentrant-reload")!.Descriptor.Generation);
        Assert.Equal(2, packageCatalog.QueryCount);
    }

    private static (NuplaneRefreshCoordinator Coordinator, TestRuntimeFeatureCatalog Catalog, IShellRegistry Registry, TestShellRegistry RegistryState)
        CreateCoordinator(NuplaneIntegrationOptions options)
    {
        var catalog = new TestRuntimeFeatureCatalog();
        var registry = TestShellRegistry.Create(out var registryState);
        var coordinator = new NuplaneRefreshCoordinator(catalog, Options.Create(options), () => registry);
        return (coordinator, catalog, registry, registryState);
    }
}
