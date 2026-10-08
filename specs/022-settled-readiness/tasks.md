# Tasks: Observe Settled Active Generations

**Input**: Design documents from `/specs/022-settled-readiness/`  
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contract](contracts/ISettledShellRegistry.md), [quickstart.md](quickstart.md)

Tests are required by constitution V; author them with implementation, then run a causal mutation. No TDD obligation is inferred. Spec Kit's newer setup-tasks script is absent; the repository's consolidated check-prerequisites script provides the same validated feature paths and available documents. No extension hooks are installed.

## Phase 1: Setup

- [X] T001 Validate requirements and constitution, record the existing settlement authority and deferred independent pruning decision in `specs/022-settled-readiness/research.md`.

## Phase 2: Foundational Contract

- [X] T002 Define the optional public capability and full XML documentation in `src/CShells.Abstractions/Lifecycle/ISettledShellRegistry.cs`; keep `IShellRegistry` and DI descriptors unchanged.

## Phase 3: User Story 1 - Observe readiness without starting work (P1)

**Goal**: Synchronous nonactivating settled observation for cold, pending, successful, and rejected initial activation.
**Independent Test**: Observe while a real participant holds Commit and Complete; return null before release and the exact generation after settlement, with zero cold lookup/build work.

- [X] T003 [US1] Add initial activation, cold/unknown, invalid-name/case, blocked Commit/Complete, throwing Complete, and optional custom-registry coverage in `tests/CShells.Tests/Integration/Lifecycle/ShellRegistrySettledObservationTests.cs`.
- [X] T004 [US1] Implement the capability on `src/CShells/Lifecycle/ShellRegistry.cs` using existing marker/current-active identity and eligibility, without provider lookup, semaphore wait, callback, or new retained state.

## Phase 4: User Story 2 - Observe current readiness across reload (P1)

**Goal**: Current-generation observations follow successful reload and rollback without certifying provisional replacements or historical shells.
**Independent Test**: Gate before and after publication, then exercise success, rollback, and direct drain/removal during completion.

- [X] T005 [US2] Add reload composition, pending commit/completion, success/rollback, drain/unregister/final-eligibility coverage in `tests/CShells.Tests/Integration/Lifecycle/ShellRegistrySettledObservationTests.cs`; ensure every gate and owned operation is released/joined during teardown.
- [X] T006 [US2] Verify rechecks cover removed/replaced name-slot identity in `src/CShells/Lifecycle/ShellRegistry.cs` and document only point-in-time semantics.

## Phase 5: Polish and Qualification

- [X] T007 [P] Document capability casting, unsupported versus empty distinction, diagnostic-only Complete errors, no activation, no use lease, and unchanged routing/runner semantics in `src/CShells/README.md` and `src/CShells.Abstractions/README.md`.
- [ ] T008 Run focused observation, existing settlement/runner/lifecycle tests, full `tests/CShells.Tests/CShells.Tests.csproj`, `tests/CShells.Tests.EndToEnd/CShells.Tests.EndToEnd.csproj`, and three-target `src/CShells/CShells.csproj` build; save exact-head evidence in `specs/022-settled-readiness/quickstart.md`.
- [ ] T009 Root independently reviews the exact delta and runs a compiled marker-bypass mutation/revert proof against `tests/CShells.Tests/Integration/Lifecycle/ShellRegistrySettledObservationTests.cs`; record results in `specs/022-settled-readiness/quickstart.md`.
- [ ] T010 Root publishes one org-branch PR, merges only after exact-head review and required hosted gates, verifies main and all ten published preview packages, then qualifies a PackageReference-only external consumer on net8/net9/net10; record provenance/results in `specs/022-settled-readiness/quickstart.md` and the owning issue. Keep Foundation stable adoption acceptance separate.

## Dependencies and Execution Order

T001 precedes T002. US1 establishes the public method; US2 exercises replacement behavior through that same method and follows it. T007 may be authored independently after the contract. T008 and T009 follow final implementation; T010 requires their passing evidence and hosted checks. Only documentation review can use a parallel lane; keep product/test edits together to avoid competing writes to one registry/test fixture. MVP is US1, but this issue delivers both stories before publication.

## Implementation Strategy

Implement the optional contract and smallest read-only method, build coherent deterministic regressions, review cleanup/concurrency, run focused then full gates once on the final candidate, and retain the public-package consumer as the post-publication gate. Do not edit Foundation pins, host policy, stable versions, Nuplane, or pruning/unloading.
