# Research: Runtime Feature Catalog Commit Notifications

## Decision: Expose an optional capability with the detailed snapshot as the event payload

- **Decision**: Add `IRuntimeFeatureCatalogCommitSource` in `CShells.Abstractions` with an event whose payload is the exact `RuntimeFeatureCatalogSnapshot`. Implement the capability on the stock `RuntimeFeatureCatalogAccessor` only; do not add members to `IRuntimeFeatureCatalog` and do not register a separate DI alias.
- **Rationale**: Existing consumers and custom catalog implementations remain source-compatible. Consumers capability-test the object returned by resolving `IRuntimeFeatureCatalog`, so a host override cannot silently leave them observing a different catalog through an alias.
- **Alternatives considered**: Add an event to `IRuntimeFeatureCatalog` (breaks custom implementation requirements); register `IRuntimeFeatureCatalogCommitSource` separately (can point at the stock instance while resolution of `IRuntimeFeatureCatalog` returns a replacement); publish only the reduced snapshot projection (loses assemblies, feature map, and startup metadata).

## Decision: Commit before enqueue, and dispatch outside the refresh semaphore

- **Decision**: While holding the existing refresh semaphore, finish discovery, check cancellation before commit, publish the immutable snapshot with `Volatile.Write`, then enqueue that snapshot under a short notification gate. Release the refresh semaphore before draining callbacks.
- **Rationale**: The queue order matches commit order, lock-free readers observe the commit before a handler can run, and handlers never execute while the asynchronous refresh gate is held. Cancellation before publication has no observable commit; cancellation after publication cannot roll it back.
- **Alternatives considered**: Invoke callbacks inside the refresh lock (deadlocks reentrant refresh and serializes commits behind user code); enqueue after releasing the lock (can reorder concurrent commits); read `CurrentSnapshot` without a volatile publication/read (weaker visibility guarantee).

## Decision: One synchronous FIFO drainer with a short handoff gate

- **Decision**: Maintain a FIFO queue and a `dispatching` flag protected by one private gate. Enqueue/dequeue and the transition between an empty queue and `dispatching = false` happen under that same gate. The drainer releases the gate before invoking any callback. Concurrent/reentrant refreshes enqueue behind an active drainer and do not wait for its callback to finish.
- **Rationale**: Atomic empty-check/handoff prevents a producer from stranding a queued commit between the last dequeue and drainer shutdown. A synchronous callback can reenter `RefreshAsync` because the refresh semaphore is released; that refresh returns while its notification remains queued for the active drainer.
- **Alternatives considered**: A background hosted dispatcher (unneeded lifetime/service machinery); awaiting every notification task as a commit transaction (subscriber work delays refresh callers); a compare-and-swap loop (more complex than the short gate and queue state require).

## Decision: Isolate each subscriber and document no replay

- **Decision**: Snapshot the event invocation list for each committed notification and invoke each handler separately. Catch and log each subscriber exception, then continue. Do not replay previously committed snapshots to newly added handlers.
- **Rationale**: One host callback cannot suppress another callback or fail a committed refresh. No-replay keeps subscription semantics small; a late subscriber can subscribe and then read `CurrentSnapshot` to reconcile, while accepting the ordinary race that a commit can land between those actions.
- **Alternatives considered**: Standard multicast invocation without per-handler isolation; automatic replay (requires cursor/race semantics not requested); callback completion as a transaction boundary.

## Spec Kit numbering correction evidence (unrelated tooling issue)

The official sequential feature script was invoked once with:

```sh
.specify/scripts/bash/create-new-feature.sh 'Publish optional notifications for committed runtime feature catalog snapshots' --json --short-name 'runtime-catalog-commits'
```

It returned `016-runtime-catalog-commits` even though the shared Git repository already contained branch `016-share-host-singletons` checked out in another worktree. `git branch -a` prefixes that worktree branch with `+`; `.specify/scripts/bash/create-new-feature.sh:119-123` strips only leading `*` and spaces at line 120, then tests for a three-digit prefix at line 123. The leading `+` prevents the existing `016-` branch from being counted. The branch and generated spec directory were corrected manually to `017-runtime-catalog-commits` / `specs/017-runtime-catalog-commits` without rerunning the script. This evidence is recorded for a separate tooling fix and is not part of this feature's production changes.
