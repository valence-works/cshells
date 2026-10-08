namespace CShells.Features;

/// <summary>
/// Optional capability for observing successfully committed runtime feature catalog snapshots.
/// </summary>
/// <remarks>
/// Resolve <see cref="IRuntimeFeatureCatalog"/> and test whether the resolved instance implements this interface.
/// Notifications are not replayed; subscribe before catalog initialization or subscribe and then reconcile from the
/// current snapshot. A commit can occur during that reconciliation, so compare generations to avoid missing or
/// processing the same generation twice.
/// </remarks>
public interface IRuntimeFeatureCatalogCommitSource
{
    /// <summary>
    /// Occurs after a runtime feature catalog snapshot is committed and made available as the current snapshot.
    /// </summary>
    /// <remarks>
    /// The payload is the exact detailed snapshot committed, including its generation. Callbacks run synchronously
    /// outside the catalog refresh lock and should enqueue expensive work elsewhere. Each successful initial or later
    /// refresh produces one notification; existing commits are not replayed to new subscribers. A concurrent or reentrant
    /// refresh can commit and return while its notification waits behind a currently executing callback. The current
    /// snapshot can therefore be newer than the event payload.
    /// </remarks>
    event Action<RuntimeFeatureCatalogSnapshot>? SnapshotCommitted;
}
