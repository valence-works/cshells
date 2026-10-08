# Tasks: Share Host Singletons with Shells

**Input**: Design documents from `specs/016-share-host-singletons/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`

## Phase 1: Setup

**Purpose**: Locate the current root-to-shell copy behavior and reusable integration fixtures.

- [X] T001 Inspect root service copy behavior and shell integration fixture conventions in `src/CShells/Lifecycle/ShellProviderBuilder.cs` and `tests/CShells.Tests/Integration/`.

## Phase 2: User Story 1 - Share a selected host singleton safely (Priority: P1)

**Goal**: Selected unkeyed singleton registrations resolve to root-owned objects in every shell generation, while all unselected behavior remains intact.

**Independent Test**: Build multiple real shell generations and verify instance identity, ordering, disposal ownership, keyed behavior, and feature registration precedence.

### Tests for User Story 1

- [X] T002 [US1] Add integration coverage for root/type/factory/caller-instance singleton registrations, ordered enumerable identity, two shells and overlapping generations, synchronous and asynchronous root disposal exactly once, unselected default singleton copying, keyed descriptors, and feature overrides in `tests/CShells.Tests/Integration/DependencyInjection/ShareSingletonWithShellsTests.cs`.

### Implementation for User Story 1

- [X] T003 [US1] Add documented generic and runtime `Type` selection overloads and track selected closed service types in `src/CShells/DependencyInjection/CShellsBuilder.cs`.
- [X] T004 [US1] Pass selections to `ShellProviderBuilder` and resolve each selected unkeyed singleton set once per shell copy, replacing only those descriptors with instance registrations in descriptor order in `src/CShells/DependencyInjection/ServiceCollectionExtensions.cs` and `src/CShells/Lifecycle/ShellProviderBuilder.cs`.
- [X] T005 [US1] Document host-owned singleton sharing and caller-provided instance ownership in `src/CShells/README.md`.

## Phase 3: User Story 2 - Reject invalid sharing requests clearly (Priority: P2)

**Goal**: Missing, mixed-lifetime, open-generic, and root-only excluded selections fail with actionable diagnostics.

**Independent Test**: Build shells for each invalid registration selection and assert the failure names the service type and explains the allowed selection.

### Tests for User Story 2

- [X] T006 [US2] Add integration tests for missing registrations, mixed/non-singleton registrations, open generic types, and excluded selections in `tests/CShells.Tests/Integration/DependencyInjection/ShareSingletonWithShellsTests.cs`.

### Implementation for User Story 2

- [X] T007 [US2] Validate selected types and final root descriptor sets, preserve keyed descriptors, and reject root-only exclusion conflicts with teaching diagnostics in `src/CShells/Lifecycle/ShellProviderBuilder.cs`.

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Check all paths and confirm the ownership guarantee is regression-protected.

- [X] T008 Review public XML docs and focused source/test diff for redundant code in `src/CShells/DependencyInjection/CShellsBuilder.cs`, `src/CShells/Lifecycle/ShellProviderBuilder.cs`, and `tests/CShells.Tests/Integration/DependencyInjection/ShareSingletonWithShellsTests.cs`.
- [X] T009 Run focused integration tests for `tests/CShells.Tests/` and a single ownership mutation/revert proof.
- [X] T010 Validate the implementation scenarios in `specs/016-share-host-singletons/quickstart.md` and confirm no behavior outside the selected service types changed.

## Dependencies & Execution Order

- T001 precedes all feature work.
- T002 precedes the implementation tasks as the regression contract.
- T003 precedes T004; T006 follows the API wiring and precedes T007.
- T005 is independent of implementation after the API contract is settled.
- T008–T010 follow all implementation tasks.
- User Story 1 must complete before User Story 2 because both stories extend the same service selection and copy path.

## Implementation Strategy

Complete the identity/ownership behavior first, then add validation coverage and diagnostics, followed by docs, review, and focused verification. Keep edits confined to the builder, provider-copy path, integration tests, package README, and this spec.
