using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Features;

internal sealed class RuntimeFeatureCatalog(
    Func<CancellationToken, Task<IReadOnlyCollection<Assembly>>> assemblyResolver,
    ILogger<RuntimeFeatureCatalog>? logger = null)
{
    private readonly Func<CancellationToken, Task<IReadOnlyCollection<Assembly>>> assemblyResolver = Guard.Against.Null(assemblyResolver);
    private readonly ILogger<RuntimeFeatureCatalog> logger = logger ?? NullLogger<RuntimeFeatureCatalog>.Instance;
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private readonly object notificationGate = new();
    private readonly Queue<RuntimeFeatureCatalogSnapshot> pendingNotifications = new();

    private RuntimeFeatureCatalogSnapshot? currentSnapshot;
    private long nextGeneration;
    private bool isDispatchingNotifications;

    public event Action<RuntimeFeatureCatalogSnapshot>? SnapshotCommitted;

    public RuntimeFeatureCatalogSnapshot CurrentSnapshot => Volatile.Read(ref currentSnapshot)
        ?? throw new InvalidOperationException("The runtime feature catalog has not been initialized.");

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref currentSnapshot) is not null)
            return;

        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RuntimeFeatureCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return CurrentSnapshot;
    }

    public async Task<RuntimeFeatureCatalogSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var discoveryWarnings = new List<(Assembly Assembly, Exception Error)>();
        var dispatchNotifications = false;
        RuntimeFeatureCatalogSnapshot snapshot;

        try
        {
            var assemblies = await assemblyResolver(cancellationToken).ConfigureAwait(false);
            var descriptors = FeatureDiscovery
                .DiscoverFeatures(
                    assemblies,
                    (assembly, ex) => discoveryWarnings.Add((assembly, ex)))
                .ToList()
                .AsReadOnly();

            var featureMap = descriptors.ToDictionary(descriptor => descriptor.Id, descriptor => descriptor, StringComparer.OrdinalIgnoreCase);
            cancellationToken.ThrowIfCancellationRequested();

            snapshot = new RuntimeFeatureCatalogSnapshot(
                Interlocked.Increment(ref nextGeneration),
                assemblies.ToList().AsReadOnly(),
                descriptors,
                featureMap,
                DateTimeOffset.UtcNow);

            Volatile.Write(ref currentSnapshot, snapshot);
            lock (notificationGate)
            {
                pendingNotifications.Enqueue(snapshot);
                if (!isDispatchingNotifications)
                {
                    isDispatchingNotifications = true;
                    dispatchNotifications = true;
                }
            }
        }
        finally
        {
            refreshLock.Release();
            foreach (var (assembly, error) in discoveryWarnings)
            {
                LogSafely(() => logger.LogWarning(error, "Failed to load types from assembly {AssemblyName}. Features in this assembly will not be available.", assembly.GetName().Name));
            }
        }

        LogSafely(() => logger.LogInformation(
            "Committed runtime feature catalog generation {Generation} with {FeatureCount} feature(s): {FeatureNames}",
            snapshot.Generation,
            snapshot.FeatureDescriptors.Count,
            string.Join(", ", snapshot.FeatureDescriptors.Select(feature => feature.Id))));

        if (dispatchNotifications)
            DrainNotifications();

        return snapshot;
    }

    private void DrainNotifications()
    {
        while (true)
        {
            RuntimeFeatureCatalogSnapshot snapshot;
            lock (notificationGate)
            {
                if (pendingNotifications.Count == 0)
                {
                    isDispatchingNotifications = false;
                    return;
                }

                snapshot = pendingNotifications.Dequeue();
            }

            var subscribers = SnapshotCommitted;
            if (subscribers is null)
                continue;

            foreach (var subscriber in subscribers.GetInvocationList().Cast<Action<RuntimeFeatureCatalogSnapshot>>())
            {
                try
                {
                    subscriber(snapshot);
                }
                catch (Exception exception)
                {
                    LogSafely(() => logger.LogError(
                        exception,
                        "A runtime feature catalog commit subscriber failed for generation {Generation}.",
                        snapshot.Generation));
                }
            }
        }
    }

    private static void LogSafely(Action log)
    {
        try
        {
            log();
        }
        catch
        {
            // Logging must not prevent a committed snapshot from being returned or dispatched.
        }
    }
}
