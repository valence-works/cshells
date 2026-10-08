# Tasks: Runtime Feature Catalog Commit Notifications

**Input**: Design documents from `specs/017-runtime-catalog-commits/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`

## Phase 1: Setup

**Purpose**: Reuse the existing catalog and accessor test fixtures and confirm package documentation locations.

- [X] T001 Inspect current catalog/accessor unit tests and both project README conventions in `tests/CShells.Tests/Unit/Features/`, `src/CShells.Abstractions/README.md`, and `src/CShells/README.md`.

## Phase 2: User Story 1 - Observe committed catalog snapshots (Priority: P1)

**Goal**: Consumers can capability-test the resolved stock catalog and receive each committed detailed snapshot without changing the existing catalog contract.

**Independent Test**: Subscribe to an uninitialized stock catalog, initialize and refresh it, and assert the exact detailed snapshot and increasing generations; confirm late subscription has no replay and a custom legacy catalog still works.

### Tests for User Story 1

- [X] T002 [US1] Add tests for optional capability exposure, initial and subsequent exact-snapshot events, no replay, repeated `EnsureInitializedAsync` without a new event, late custom `IRuntimeFeatureCatalog` replacement, and a legacy implementation without the optional contract in `tests/CShells.Tests/Unit/Features/RuntimeFeatureCatalogCommitSourceTests.cs` and `tests/CShells.Tests/Unit/Features/RuntimeFeatureCatalogAccessorTests.cs`.

### Implementation for User Story 1

- [X] T003 [US1] Add XML-documented optional `IRuntimeFeatureCatalogCommitSource` with a detailed snapshot event in `src/CShells.Abstractions/Features/IRuntimeFeatureCatalogCommitSource.cs`.
- [X] T004 [US1] Implement event forwarding on the stock accessor only so capability testing the resolved `IRuntimeFeatureCatalog` finds the optional source without adding a separate DI alias in `src/CShells/Features/RuntimeFeatureCatalogAccessor.cs`.
- [X] T005 [US1] Document capability testing, no-replay, late-subscription reconciliation, and exact detailed payload in `src/CShells.Abstractions/README.md` and `src/CShells/README.md`.

## Phase 3: User Story 2 - Keep refreshes reliable while consumers react (Priority: P2)

**Goal**: Successful commits enqueue ordered notifications, handlers execute outside the refresh lock, failures are isolated, and concurrent/reentrant refreshes cannot deadlock or lose queued commits.

**Independent Test**: Use barriers and callback-controlled refreshes to verify commit visibility, ordered exactly-once dispatch, nonblocking commits, failure isolation, cancellation/failure behavior, reentrancy, unsubscribe, and dispatcher handoff.

### Tests for User Story 2

- [X] T006 [US2] Add deterministic unit tests for concurrent queued commits while a handler is blocked, reentrant refresh, event invocation-list isolation, unsubscribe, discovery failure, precommit cancellation, exact current-snapshot visibility, and repeated handoff batches in `tests/CShells.Tests/Unit/Features/RuntimeFeatureCatalogCommitSourceTests.cs`.

### Implementation for User Story 2

- [X] T007 [US2] Add explicit volatile snapshot publication/read, cancellation check immediately before commit, FIFO queue insertion under the refresh semaphore, and a short atomic drainer-handoff gate in `src/CShells/Features/RuntimeFeatureCatalog.cs`.
- [X] T008 [US2] Drain callbacks only outside both gates in commit order, logging and isolating every subscriber exception in `src/CShells/Features/RuntimeFeatureCatalog.cs`.

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Review public semantics, regression coverage, and package-level documentation.

- [X] T009 Review API XML docs, queue lock ordering, and DRY test fixtures in `src/CShells.Abstractions/Features/IRuntimeFeatureCatalogCommitSource.cs`, `src/CShells/Features/RuntimeFeatureCatalog.cs`, and `tests/CShells.Tests/Unit/Features/RuntimeFeatureCatalogCommitSourceTests.cs`.
- [X] T010 Run focused new and existing catalog/accessor tests, a subscriber-failure isolation mutation/revert proof, and the CShells net8.0/net9.0/net10.0 build.
- [X] T011 Validate the scenarios in `specs/017-runtime-catalog-commits/quickstart.md` and verify no mandatory catalog member or separate DI alias was added.

## Dependencies & Execution Order

- T001 precedes feature work.
- T002 precedes T003–T004; T003 precedes the stock accessor implementation in T004.
- T006 precedes queue implementation in T007–T008; tests and implementation may be developed in the same file-sensitive sequence.
- T005 follows the final capability contract; T009–T011 follow all implementation and documentation changes.
- User Story 1 precedes User Story 2 because queued notifications depend on the optional capability introduced by the first story.

## Implementation Strategy

Deliver the optional source and exact event payload first, then add the commit-ordered queue and dispatch guarantees. Validate the feature with controlled barriers and existing catalog/accessor tests before final source and contract review.
