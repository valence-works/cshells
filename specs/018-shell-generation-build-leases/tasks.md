# Tasks: Shell Generation Build Leases

**Input**: Design documents from `specs/018-shell-generation-build-leases/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

## Phase 1: Setup and foundational contract

**Purpose**: Lock the actual call graph and add the optional abstractions/root-registration seam.

- [x] T001 Confirm the approved caller set and reuse points in `src/CShells/Lifecycle/ShellRegistry.cs`, `ShellProviderBuilder.cs`, `Shell.cs`, `ServiceCollectionExtensions.cs`, and `DefaultShellServiceExclusionProvider.cs`; record any newly found internal builder caller.
- [x] T002 Add XML-documented immutable `ShellGenerationBuildContext`, `IShellGenerationBuildParticipant`, and `IShellGenerationBuildLease` under `src/CShells.Abstractions/Lifecycle/`.
- [x] T003 Resolve build participants once in root `ShellRegistry`, preserve registration order, pass them from `src/CShells/DependencyInjection/ServiceCollectionExtensions.cs`, and exclude them in `src/CShells/Hosting/DefaultShellServiceExclusionProvider.cs`.
- [x] T004 Add the small internal idempotent `ShellGenerationBuildLeaseSet` owner in `src/CShells/Lifecycle/ShellGenerationBuildLeaseSet.cs`; it may retain descriptor and unresolved lease objects only, and must release all leases in reverse order while recording failures.

## Phase 2: User Story 1 - Participate in a precisely identified shell build (Priority: P1)

**Goal**: Reserve immutable generation identity and acquire participants before catalog access, then send them the exact selected snapshot before feature construction.

**Independent Test**: Recording participants, blueprint, catalog and feature assert generation/context identity, compose/validate/acquisition/catalog/snapshot/feature order, exact snapshot object, metadata immutability, and no acquisition on compose/name mismatch.

- [x] T005 [US1] Reserve the next unique descriptor generation and immutable metadata/context from blueprint identity before `ComposeAsync`; reject exhaustion before the cast and keep the composed-name validation in `src/CShells/Lifecycle/ShellRegistry.cs`.
- [x] T006 [US1] Invoke `BeginAsync` sequentially in root order after compose/name validation but before `ShellProviderBuilder` and catalog access; unwind prior leases on begin failure in `src/CShells/Lifecycle/ShellRegistry.cs`.
- [x] T007 [US1] Require explicit context and lease-set inputs on the sole internal `ShellProviderBuilder.BuildAsync` path, call each lease once with the exact detailed selected snapshot before feature construction, and carry identity/owner through `BuildResult` in `src/CShells/Lifecycle/ShellProviderBuilder.cs`.
- [x] T008 [US1] Add order/identity tests for compose failure, name mismatch, mutable blueprint metadata, generation overflow, participant order, catalog-before/after observations, exact snapshot identity, and callback-before-feature construction in `tests/CShells.Tests/Integration/Lifecycle/ShellGenerationBuildLeaseTests.cs`.

## Phase 3: User Story 2 - Hold protection through the full generation lifetime (Priority: P1)

**Goal**: Transfer the lease-set owner to the candidate before initialization and release only after confirmed full provider teardown.

**Independent Test**: A gated asynchronous provider dependency remains undisposed and its lease remains held throughout the `Disposed` callback and blocked provider teardown; a reload keeps old/new generation leases independent.

- [x] T009 [US2] Transfer the lease owner from build result to `Shell` before resolving initializers and add an optional internal constructor seam that preserves existing direct shell test construction in `src/CShells/Lifecycle/ShellRegistry.cs` and `src/CShells/Lifecycle/Shell.cs`.
- [x] T010 [US2] Release the owner in reverse order only after `Disposed` lifecycle notification and successful provider teardown; preserve the existing single-provider-disposal task semantics in `src/CShells/Lifecycle/Shell.cs`.
- [x] T011 [US2] Change unpublished initializer cleanup to return an explicit provider-disposal outcome, release leases only on confirmed success, preserve no-transition/no-double-disposal behavior, and keep initializer exceptions primary in `src/CShells/Lifecycle/ShellRegistry.cs` and `Shell.cs`.
- [x] T012 [US2] Add deterministic tests for initial ownership, old/new overlap during drain, slow provider disposal after the early `Disposed` event, initializer cleanup success/failure, and release order in `tests/CShells.Tests/Integration/Lifecycle/ShellGenerationBuildLeaseTests.cs` and existing initializer/reload suites.

## Phase 4: User Story 3 - Keep unresolved leases rooted when cleanup is uncertain (Priority: P1)

**Goal**: Retain unresolved protection independently of shell slot history while preserving primary failures and deterministic cleanup diagnostics.

**Independent Test**: Inject teardown and release failures; verify no release before confirmed teardown, all eligible leases attempted, failed leases remain strongly rooted after GC, provider/shell are not rooted by the retention collection, and primary construction/activation errors remain primary.

- [x] T013 [US3] Add a private synchronized retention collection on root-lifetime `ShellRegistry`; retain unresolved owner/descriptor only (never shell/provider/snapshot) on lifecycle or provider teardown failure in `src/CShells/Lifecycle/ShellRegistry.cs`.
- [x] T014 [US3] Ensure release failures attempt every lease, retain only failed lease objects, aggregate deterministically on successful teardown, and use safe cleanup logging to prevent logger exceptions masking build/initializer/activation errors in `src/CShells/Lifecycle/ShellGenerationBuildLeaseSet.cs`, `Shell.cs`, and `ShellRegistry.cs`.
- [x] T015 [US3] Preserve the original activation failure if candidate `DisposeAsync` also fails; ensure failed candidate owners are root-retained before cleanup returns in `src/CShells/Lifecycle/ShellRegistry.cs`.
- [x] T016 [US3] Add fault/retention tests for later begin failure, snapshot callback failure/cancellation, feature build failure, activation rollback plus disposal failure, lifecycle notification failure, provider teardown failure plus forced GC, multiple reverse release faults, cleanup logging failure, cancelled-token cleanup, distinct-name concurrency, root-only exclusion and no-participant behavior in `tests/CShells.Tests/Integration/Lifecycle/ShellGenerationBuildLeaseTests.cs` and targeted lifecycle/DI suites.

## Phase 5: Polish and validation

**Purpose**: Complete public docs, prove the core ordering invariant, and prepare a reviewable local change.

- [x] T017 Document API lifetime, exact snapshot, callback order, same-name reentry limitation, failed-release contract and no unloading/deletion guarantee in XML comments and `src/CShells.Abstractions/README.md` / `src/CShells/README.md`.
- [x] T018 Run focused lease/activation/initializer/reload/terminator tests and relevant existing no-participant regressions in `tests/CShells.Tests/`.
- [x] T019 Perform the required mutation/revert: temporarily release leases during the early `Disposed` transition and confirm the gated slow-provider-disposal test fails; restore and rerun it successfully.
- [x] T020 Run `dotnet build src/CShells/CShells.csproj` for net8.0/net9.0/net10.0, inspect the complete diff and run `git diff --check`.
- [x] T021 Validate the scenarios in `specs/018-shell-generation-build-leases/quickstart.md` and confirm every task is complete; recheck issue #147 comments/open PRs before the local commit.

## Dependencies and execution order

- T001 precedes implementation; T002–T004 establish the optional contract and ownership primitive.
- US1 tasks T005–T008 depend on the foundational contract; the participant callback path must be established before ownership is transferred.
- US2 tasks T009–T012 depend on the owner carried by `BuildResult`.
- US3 tasks T013–T016 depend on the owner and release path so failures can be retained consistently.
- T017–T021 follow the implementation. The required full-project test gate and final review are root-owned.

## Parallel opportunities

The feature is intentionally one serial lifecycle change. Contract docs and unrelated test fixtures could be authored separately, but no implementation tasks are marked parallel because they touch shared lifecycle seams and ownership boundaries.

## Implementation strategy

Build in this order: public contract and root registration; identity/acquisition/exact-snapshot callback; lease ownership transfer and confirmed teardown release; root retention and primary-error handling; integration proofs, docs, mutation/revert and multi-target build. This keeps each stage reviewable and leaves no public API consumer dependent on an incomplete teardown rule.
