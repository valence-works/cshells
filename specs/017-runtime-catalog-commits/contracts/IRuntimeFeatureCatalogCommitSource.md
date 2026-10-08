# Contract: Optional Runtime Feature Catalog Commit Notifications

## Capability

Consumers resolve `IRuntimeFeatureCatalog` from the host root provider and test whether that resolved object also implements `IRuntimeFeatureCatalogCommitSource`. The existing `IRuntimeFeatureCatalog` contract gains no mandatory members, and CShells registers no independent source alias.

## Event

The optional source exposes `SnapshotCommitted`, an event carrying the exact detailed `RuntimeFeatureCatalogSnapshot` published by a successful commit. The payload includes its generation, discovery assemblies, descriptors, feature map, and refresh timestamp.

## Timing and delivery

- Initial discovery is a commit and produces one notification.
- Each later successful refresh produces exactly one notification. Failed or pre-commit-cancelled discovery produces none.
- `CurrentSnapshot` is published before the notification is queued and before a handler runs.
- Commits and queue insertions are serialized in generation order. A single dispatcher invokes handlers outside the refresh semaphore.
- A concurrent or reentrant refresh may commit and return while an earlier notification callback remains active; its event is delivered later in FIFO order.
- One subscriber exception is logged and isolated; other subscribers and later notifications still run.
- A handler added after a commit is not eligible for that commit, even if its notification is still queued. Dispatch samples active eligible handlers when it dequeues each snapshot.
- Removing a handler before a queued snapshot's handlers are sampled prevents that delivery. Once the invocation list is sampled for an in-flight snapshot, later add/remove operations do not change that list.

## Replay and races

Subscriptions receive no replay. Consumers that subscribe after the source has initialized should subscribe first and then read `CurrentSnapshot` to reconcile. A concurrent commit may occur between those operations; consumers can compare the event generation with the snapshot generation and tolerate duplicates or gaps according to their needs. The event payload always remains the exact committed snapshot even if `CurrentSnapshot` has advanced.

Handlers are synchronous and should enqueue expensive work elsewhere. Avoid blocking refresh-related work in a handler; reentrant refresh is safe, but a callback that performs slow work holds the single notification drainer until it returns.
