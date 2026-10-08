using CShells.Lifecycle;
using CShells.Nuplane;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nuplane.Abstractions;
using Nuplane.Events;
using Nuplane.Observability;

namespace CShells.Tests.Integration.Nuplane;

public sealed class NuplaneObserverDiagnosticsTests
{
    [Fact]
    public async Task RealDispatcherLogsAdapterErrorThenWarningAndContinuesToFollowingObserver()
    {
        var loggerProvider = new NuplaneTestLoggerProvider();
        var refreshFailure = new InvalidOperationException("dispatcher refresh failure");
        var catalog = new TestRuntimeFeatureCatalog
        {
            RefreshHandler = (number, _) => number == 1
                ? Task.FromException(refreshFailure)
                : Task.CompletedTask
        };
        var registry = TestShellRegistry.Create(out _);
        var followingObserver = new FollowingObserver();
        var services = NuplaneCoordinatorTestCases.CreateAdapterServices(catalog, registry, loggerProvider);
        services.AddSingleton<INuplaneObserver>(followingObserver);
        services.AddSingleton<ReconciliationLogger>();
        services.AddSingleton<IReconciliationLogger>(provider => provider.GetRequiredService<ReconciliationLogger>());
        services.AddSingleton<ObserverEventDispatcher>();
        services.AddSingleton<IObserverEventDispatcher>(provider => provider.GetRequiredService<ObserverEventDispatcher>());

        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IObserverEventDispatcher>();
        var applied = FakePackageAssemblyCatalog.Resolved("feature-package");
        const string correlationId = "dispatcher-diagnostic-correlation";
        var changeSet = new PackageChangeSet([applied], [], [], correlationId, DateTimeOffset.UtcNow);

        await dispatcher.PublishReconciledAsync(changeSet, [applied], CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, catalog.RefreshCount);
        Assert.Equal(1, followingObserver.ReconciledCalls);
        var firstLogs = loggerProvider.Snapshot().ToArray();
        var error = Assert.Single(firstLogs, entry => entry.Level == LogLevel.Error);
        var warning = Assert.Single(firstLogs, entry => entry.Level == LogLevel.Warning);
        Assert.Same(refreshFailure, error.Exception);
        Assert.Equal(nameof(INuplaneObserver.OnPackagesReconciledAsync), error.Properties["Operation"]);
        Assert.Equal(correlationId, error.Properties["CorrelationId"]);
        Assert.Equal(correlationId, warning.Properties["CorrelationId"]);
        Assert.Contains(refreshFailure.Message, warning.Message, StringComparison.Ordinal);
        Assert.True(Array.IndexOf(firstLogs, error) < Array.IndexOf(firstLogs, warning));

        var retry = new PackageChangeSet([], [], [], "dispatcher-retry", DateTimeOffset.UtcNow);
        await dispatcher.PublishReconciledAsync(retry, [applied], CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, catalog.RefreshCount);
        Assert.Equal(2, followingObserver.ReconciledCalls);
        Assert.Single(loggerProvider.Snapshot(), entry => entry.Level == LogLevel.Error);
        Assert.Single(loggerProvider.Snapshot(), entry => entry.Level == LogLevel.Warning);
    }

    private sealed class FollowingObserver : INuplaneObserver
    {
        private int _reconciledCalls;

        public int ReconciledCalls => _reconciledCalls;

        public Task OnPackagesChangingAsync(PackageChangeSet changeSet, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OnPackagesChangedAsync(PackageChangeSet changeSet, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OnPackageFailedAsync(string packageId, Exception exception, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OnPackagesReconciledAsync(
            PackageChangeSet changeSet,
            IReadOnlyList<ResolvedPackage> appliedPackages,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _reconciledCalls);
            return Task.CompletedTask;
        }
    }
}
