# Feature Specification: Runtime Feature Catalog Commit Notifications

**Feature Branch**: `017-runtime-catalog-commits`
**Created**: 2026-10-08
**Status**: Draft
**Input**: User description: "Publish optional notifications for committed runtime feature catalog snapshots."

## User Scenarios & Testing

### User Story 1 - Observe committed catalog snapshots (Priority: P1)

A consumer that needs to react to runtime feature discovery can capability-test the resolved catalog for an optional commit-notification source and subscribe to exact detailed snapshots without changing compatibility for older custom catalog implementations.

**Why this priority**: Consumers can react to successful catalog refreshes without polling and can use the complete metadata that was committed.

**Independent Test**: Subscribe to the stock catalog before initialization, trigger initial discovery and a later refresh, and verify one detailed event for each commit with matching generation and payload.

**Acceptance Scenarios**:

1. **Given** the stock catalog is not initialized, **When** a consumer subscribes and initializes it, **Then** one notification carries the initial committed detailed snapshot.
2. **Given** an initialized catalog, **When** a successful refresh commits, **Then** one notification carries that exact committed snapshot, including its generation and discovery metadata.
3. **Given** a custom implementation of the existing catalog contract, **When** the consumer capability-tests it, **Then** the implementation remains valid without implementing the optional notification contract.

### User Story 2 - Keep refreshes reliable while consumers react (Priority: P2)

A consumer can perform more catalog work during notification handling without blocking refresh commits or losing ordered notifications. A faulty consumer cannot prevent other consumers or a successful refresh from completing.

**Why this priority**: Notifications run in extensible host code, so they must not corrupt refresh state, deadlock reentrant refreshes, or hide later committed generations.

**Independent Test**: Use controlled subscriber barriers and concurrent refresh requests to prove commits remain available while dispatch is paused, notifications are delivered in order exactly once, and one throwing subscriber does not suppress another.

**Acceptance Scenarios**:

1. **Given** a notification handler is still running, **When** another refresh commits, **Then** the refresh can complete without waiting on the handler and its notification remains queued in generation order.
2. **Given** a notification handler refreshes the catalog, **When** that nested refresh commits, **Then** it returns without deadlock and its notification is delivered after the current callback.
3. **Given** one subscriber throws, **When** a snapshot is dispatched, **Then** the exception is logged, other subscribers receive the snapshot, and the committed refresh remains successful.
4. **Given** discovery fails or is cancelled before commit, **When** the attempt ends, **Then** the current snapshot is unchanged and no notification is emitted.
5. **Given** a consumer unsubscribes, **When** later commits are dispatched, **Then** that consumer receives no further notifications.

## Edge Cases

- Notifications are not replayed to a subscriber that registers after a commit. Such a consumer should subscribe before initialization or read `CurrentSnapshot` after subscribing to reconcile the race.
- With concurrent commits, a callback for generation N may observe `CurrentSnapshot` at a later generation; its event payload remains the exact snapshot for N.
- Cancellation after a snapshot commits cannot undo the committed snapshot or retract its queued notification.
- Repeated `EnsureInitializedAsync` or snapshot reads after initialization do not create commits or notifications.
- Subscriber callbacks must be quick; they should enqueue expensive work elsewhere because the active drainer invokes synchronous callbacks.

## Requirements

### Functional Requirements

- **FR-001**: The framework MUST expose an optional notification capability separate from the existing runtime-catalog interface.
- **FR-002**: The stock catalog service MUST support capability testing for committed-snapshot notifications without requiring custom catalog implementations to add members.
- **FR-003**: Each successful initial or subsequent commit MUST enqueue exactly one notification containing the detailed immutable snapshot that was committed.
- **FR-004**: Snapshot publication and queue insertion MUST be serialized with commits, and subscriber callbacks MUST run after the refresh lock is released.
- **FR-005**: Notifications MUST be dispatched one at a time in commit-generation order, with each queued commit delivered exactly once.
- **FR-006**: A subscriber exception MUST be logged and isolated so that it does not fail refresh or suppress delivery to other subscribers.
- **FR-007**: Concurrent or reentrant refreshes MUST NOT wait for an active notification callback to finish before committing; they may return before their queued notification is delivered.
- **FR-008**: The notification source MUST provide no replay; documentation MUST explain subscribing before initialization and reconciling late subscriptions with `CurrentSnapshot`.
- **FR-009**: A failed or pre-commit-cancelled discovery attempt MUST leave the committed snapshot and notification stream unchanged.
- **FR-010**: Unsubscribed handlers MUST not receive later notifications.
- **FR-011**: Existing typed snapshot projections, detailed snapshot reads, and existing custom `IRuntimeFeatureCatalog` implementations MUST remain source-compatible.

## Key Entities

- **Catalog commit**: A successfully published runtime feature discovery snapshot with a monotonically increasing generation.
- **Commit notification source**: An optional capability on a resolved catalog that allows consumers to subscribe to committed detailed snapshots.
- **Notification subscriber**: A consumer registered to receive future commit notifications; registration does not request a replay.
- **Pending notification**: A committed snapshot awaiting serialized dispatch outside the refresh lock.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Every successful catalog commit produces exactly one delivery to each currently subscribed handler, in generation order.
- **SC-002**: Failed or pre-commit-cancelled discovery produces zero notifications and leaves the prior committed generation unchanged.
- **SC-003**: Reentrant and concurrent refresh scenarios complete without deadlock while notification callbacks are held at deterministic barriers.
- **SC-004**: A throwing handler does not prevent any other subscribed handler from receiving the same committed snapshot.
