using CShells.Features;
using CShells.Lifecycle;
using Microsoft.Extensions.Options;
using Nuplane.Abstractions;

namespace CShells.Nuplane.Internal;

/// <summary>Coordinates deferred catalog freshness and optional automatic shell reload.</summary>
internal sealed class NuplaneRefreshCoordinator(
    IRuntimeFeatureCatalog featureCatalog,
    IOptionsMonitor<NuplaneIntegrationOptions> options,
    Func<IShellRegistry> shellRegistryFactory) : INuplaneObserver, IShellGenerationBuildParticipant
{
    private readonly IRuntimeFeatureCatalog catalog = featureCatalog ?? throw new ArgumentNullException(nameof(featureCatalog));
    private readonly IOptionsMonitor<NuplaneIntegrationOptions> optionsMonitor = options ?? throw new ArgumentNullException(nameof(options));
    private readonly Func<IShellRegistry> registryFactory = shellRegistryFactory ?? throw new ArgumentNullException(nameof(shellRegistryFactory));
    private readonly object epochStateLock = new();
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly SemaphoreSlim reloadGate = new(1, 1);

    private long _requestedCatalogEpoch;
    private long _committedCatalogEpoch;
    private long _requestedReloadEpoch;
    private long _committedReloadEpoch;
    private long _reloadCatalogEpoch;

    public Task OnPackagesChangingAsync(PackageChangeSet changeSet, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnPackagesChangedAsync(PackageChangeSet changeSet, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnPackageFailedAsync(string packageId, Exception exception, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task OnPackagesReconciledAsync(
        PackageChangeSet changeSet,
        IReadOnlyList<ResolvedPackage> appliedPackages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changeSet);
        ArgumentNullException.ThrowIfNull(appliedPackages);

        cancellationToken.ThrowIfCancellationRequested();
        if (appliedPackages.Count == 0 && changeSet.Removed.Count == 0)
            return;

        var policy = CapturePolicy();
        if (!policy.Enabled)
            return;

        var hasSourceChanges = changeSet.Added.Count > 0 || changeSet.Updated.Count > 0 || changeSet.Removed.Count > 0;
        var forceRefresh = policy.RefreshTrigger == NuplaneRefreshTrigger.EveryEligibleCompletion;
        long catalogRequestAtDelivery;
        bool callbackRequestsCatalogWork;

        lock (epochStateLock)
        {
            if (forceRefresh || hasSourceChanges)
                _requestedCatalogEpoch = checked(_requestedCatalogEpoch + 1);

            catalogRequestAtDelivery = _requestedCatalogEpoch;
            callbackRequestsCatalogWork = _requestedCatalogEpoch > _committedCatalogEpoch;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var registry = registryFactory();
        if (registry.GetActiveShells().Count == 0)
            return;

        if (policy.AutoReload && callbackRequestsCatalogWork)
        {
            lock (epochStateLock)
            {
                _requestedReloadEpoch = checked(_requestedReloadEpoch + 1);
                _reloadCatalogEpoch = catalogRequestAtDelivery;
            }
        }

        await RefreshPendingCatalogAsync(cancellationToken).ConfigureAwait(false);
        await TryReloadPendingAsync(registry, policy, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IShellGenerationBuildLease> BeginAsync(
        ShellGenerationBuildContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        await RefreshPendingCatalogAsync(cancellationToken).ConfigureAwait(false);
        return NoOpBuildLease.Instance;
    }

    private async ValueTask RefreshPendingCatalogAsync(CancellationToken cancellationToken)
    {
        await refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long capturedEpoch;
            lock (epochStateLock)
            {
                if (_requestedCatalogEpoch <= _committedCatalogEpoch)
                    return;

                capturedEpoch = _requestedCatalogEpoch;
            }

            await catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            lock (epochStateLock)
                _committedCatalogEpoch = Math.Max(_committedCatalogEpoch, capturedEpoch);
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private IntegrationPolicy CapturePolicy()
    {
        var current = optionsMonitor.CurrentValue ?? throw new InvalidOperationException(
            $"The options monitor returned null for {nameof(NuplaneIntegrationOptions)}.");
        var policy = new IntegrationPolicy(
            current.Enabled,
            current.RefreshTrigger,
            current.AutoReload,
            current.OnReloadResults);

        if (!Enum.IsDefined(policy.RefreshTrigger))
            throw new ArgumentOutOfRangeException(
                nameof(NuplaneIntegrationOptions.RefreshTrigger),
                policy.RefreshTrigger,
                $"Unsupported {nameof(NuplaneRefreshTrigger)} value. Use {NuplaneRefreshTrigger.ChangedOrPending} or {NuplaneRefreshTrigger.EveryEligibleCompletion}.");

        return policy;
    }

    private async ValueTask TryReloadPendingAsync(
        IShellRegistry registry,
        IntegrationPolicy policy,
        CancellationToken cancellationToken)
    {
        if (!policy.AutoReload)
            return;

        await reloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long capturedReloadEpoch;
            lock (epochStateLock)
            {
                if (_requestedReloadEpoch <= _committedReloadEpoch || _reloadCatalogEpoch > _committedCatalogEpoch)
                    return;

                capturedReloadEpoch = _requestedReloadEpoch;
            }

            if (registry.GetActiveShells().Count == 0)
                return;

            var results = await registry.ReloadActiveAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var stableResults = Array.AsReadOnly(results.ToArray());
            var allSucceeded = stableResults.Count > 0;
            foreach (var result in stableResults)
            {
                if (result.Error is not null)
                    allSucceeded = false;
            }

            if (policy.OnReloadResults is { } callback)
                await callback(stableResults, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (!allSucceeded)
                return;

            lock (epochStateLock)
                _committedReloadEpoch = Math.Max(_committedReloadEpoch, capturedReloadEpoch);
        }
        finally
        {
            reloadGate.Release();
        }
    }

    private readonly record struct IntegrationPolicy(
        bool Enabled,
        NuplaneRefreshTrigger RefreshTrigger,
        bool AutoReload,
        Func<IReadOnlyList<ReloadResult>, CancellationToken, ValueTask>? OnReloadResults);
}
