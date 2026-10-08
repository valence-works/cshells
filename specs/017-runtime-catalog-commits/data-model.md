# Data Model: Runtime Feature Catalog Commit Notifications

## Catalog commit

- **Identity**: One fully validated `RuntimeFeatureCatalogSnapshot` with a monotonically increasing generation.
- **Publication**: The catalog makes the snapshot visible as `CurrentSnapshot` before adding it to the notification queue.
- **Failure boundary**: Discovery failure or cancellation before publication leaves the previous snapshot and queue unchanged. Cancellation after publication cannot undo the commit.

## Commit notification source

- **Identity**: An optional capability implemented by the stock resolved `IRuntimeFeatureCatalog` accessor.
- **Payload**: The exact detailed snapshot object committed by the catalog, not the reduced typed projection.
- **Subscription**: Receives future commits only; there is no replay. Subscribe before initialization or read `CurrentSnapshot` after subscribing to reconcile a late registration.

## Pending notification

- **Identity**: A committed snapshot waiting in a FIFO queue.
- **Ordering**: Queue insertion occurs under the refresh semaphore in commit order. A single drainer removes and publishes items in the same order.
- **Handoff**: Queue empty-check and drainer shutdown are atomic under one short private gate, so new queue entries either join the active drainer or claim a new one.
- **Callback behavior**: Each handler runs outside both gates. One handler failure is logged and does not suppress other handlers or later generations.
- **Concurrency**: A refresh committed while a handler runs may return before its notification is delivered. `CurrentSnapshot` can therefore be newer than the payload currently being delivered.
