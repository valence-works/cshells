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
    private readonly List<SnapshotSubscriber> snapshotSubscribers = [];

    private RuntimeFeatureCatalogSnapshot? currentSnapshot;
    private long nextGeneration;
    private bool isDispatchingNotifications;

    public event Action<RuntimeFeatureCatalogSnapshot>? SnapshotCommitted
    {
        add => AddSubscribers(value);
        remove => RemoveSubscribers(value);
    }

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
        List<(Assembly Assembly, Exception Error)> discoveryWarnings = [];
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

            lock (notificationGate)
            {
                Volatile.Write(ref currentSnapshot, snapshot);
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
            Action<RuntimeFeatureCatalogSnapshot>[] subscribers;
            lock (notificationGate)
            {
                if (pendingNotifications.Count == 0)
                {
                    isDispatchingNotifications = false;
                    return;
                }

                snapshot = pendingNotifications.Dequeue();
                subscribers = snapshotSubscribers
                    .Where(subscriber => subscriber.FirstEligibleGeneration <= snapshot.Generation)
                    .Select(subscriber => subscriber.Handler)
                    .ToArray();
            }

            foreach (var subscriber in subscribers)
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

    private void AddSubscribers(Action<RuntimeFeatureCatalogSnapshot>? subscribers)
    {
        if (subscribers is null)
            return;

        var handlers = subscribers.GetInvocationList().Cast<Action<RuntimeFeatureCatalogSnapshot>>();
        lock (notificationGate)
        {
            var firstEligibleGeneration = currentSnapshot is null ? 1 : currentSnapshot.Generation + 1;
            foreach (var handler in handlers)
                snapshotSubscribers.Add(new SnapshotSubscriber(handler, firstEligibleGeneration));
        }
    }

    private void RemoveSubscribers(Action<RuntimeFeatureCatalogSnapshot>? subscribers)
    {
        if (subscribers is null)
            return;

        var handlers = subscribers.GetInvocationList().Cast<Action<RuntimeFeatureCatalogSnapshot>>().ToArray();
        lock (notificationGate)
        {
            for (var start = snapshotSubscribers.Count - handlers.Length; start >= 0; start--)
            {
                var matches = true;
                for (var offset = 0; offset < handlers.Length; offset++)
                {
                    if (snapshotSubscribers[start + offset].Handler == handlers[offset])
                        continue;

                    matches = false;
                    break;
                }

                if (!matches)
                    continue;

                snapshotSubscribers.RemoveRange(start, handlers.Length);
                return;
            }
        }
    }

    private sealed record SnapshotSubscriber(
        Action<RuntimeFeatureCatalogSnapshot> Handler,
        long FirstEligibleGeneration);

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
