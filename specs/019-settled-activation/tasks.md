# Tasks: Settled Shell Activation Results

**Input**: Design documents from `specs/019-settled-activation/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `quickstart.md`

## Phase 1: User Story 1 - Receive only a settled shell from activation (P1)

**Goal**: A `GetOrActivateAsync` caller does not receive a candidate until activation settles, while existing early observers and serialized activation remain intact.

**Independent Test**: Block an activation participant's `Commit` after candidate publication; prove a concurrent request waits, then prove success and rollback produce the correct settled result.

### Tests

- [x] T001 Add deterministic participant and bounded gate helpers plus initial activation success/failure settlement tests in `tests/CShells.Tests/Integration/Lifecycle/ShellRegistryActivationSettlementTests.cs`.
- [x] T002 Add reload, pre-publication serving-generation, cancellation, candidate removal during commit and completion, completion/success logging-failure, contended no-participant stampede, and different-generation outcome cases in `tests/CShells.Tests/Integration/Lifecycle/ShellRegistryActivationSettlementTests.cs`.

### Implementation

- [x] T003 Add a monotonic internal committed marker to `src/CShells/Lifecycle/Shell.cs`, with volatile publication/observation and no public `IShell` change.
- [x] T004 Update `GetOrActivateAsync` in `src/CShells/Lifecycle/ShellRegistry.cs` to return only committed shells on its fast path and recheck settlement under the existing per-name semaphore.
- [x] T005 Mark the candidate committed in `src/CShells/Lifecycle/ShellRegistry.cs` only after participant completion and the final active eligibility check, before success logging.
- [x] T006 Document the supported observation/wait behavior and same-name activation callback reentry constraint in `src/CShells/README.md`.

## Phase 2: Polish and verification

- [x] T007 Review implementation/tests for cancellation, gate cleanup, exception behavior, and unnecessary duplication; run focused settlement and activation/reload/routing tests plus the multi-target build; restore the unconditional fast path as a mutation, prove the blocked-commit test fails, then restore the fix and prove it passes.
- [x] T008 Validate `quickstart.md`, confirm every task is complete, and recheck Issue #148 comments/open PRs before the local commit.

## Dependencies and execution order

- T001/T002 establish the regression coverage. T003/T004/T005 are sequential production changes in distinct state/orchestration locations. T006 follows implementation review. T007 validates the integrated result; T008 precedes the local commit.
- No task introduces a public abstraction, runner, event, queue, or additional synchronization primitive.
