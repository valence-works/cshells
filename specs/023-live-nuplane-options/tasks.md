# Tasks: Apply Live Nuplane Integration Options

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contract](contracts/options-policy.md), [quickstart](quickstart.md)

**Prerequisites**: All documents above are present and reviewed.

**Tests**: Required by CShells constitution V. The constitution does not require test-first/TDD ordering; add deterministic tests with each implementation slice and verify them before merge.

**Task setup**: This checkout does not contain `setup-tasks.sh`. Tasks were generated from the repository's task template and verified through `check-prerequisites.sh --json --require-tasks --include-tasks`.

## Phase 1: Setup

**Purpose**: Provide a focused monitor test double shared by coordinator tests.

- [ ] T001 Add a minimal `IOptionsMonitor<NuplaneIntegrationOptions>` test helper with current-value read counting, current-value replacement, injected read failure, and the required no-op `OnChange` implementation in `tests/CShells.Tests/Integration/Nuplane/NuplaneCoordinatorTestHarness.cs`.

## Phase 2: Foundational Options Wiring

**Purpose**: Make the existing root coordinator and direct test construction resolve the standard monitor contract before implementing per-delivery behavior.

- [ ] T002 Change the root coordinator factory in `src/CShells.Nuplane/CShellsNuplaneBuilderExtensions.cs` and the constructor in `src/CShells.Nuplane/Internal/NuplaneRefreshCoordinator.cs` to use `IOptionsMonitor<NuplaneIntegrationOptions>`, then migrate direct coordinator construction in `tests/CShells.Tests/Integration/Nuplane/NuplaneRefreshCoordinatorTests.cs` and `tests/CShells.Tests/Integration/Nuplane/NuplaneReloadResultsTests.cs` to the shared helper while preserving current test behavior.

## Phase 3: User Story 1 - Apply updated settings on the next delivery (P1)

**Goal**: A root-owned singleton adapter coordinator uses the current supported options for each eligible callback and keeps one operation's choices consistent through its lifetime.

**Independent Test**: An outside or DI-composed observer receives one eligible event with reload off, configuration is reloaded to turn reload on, and a later event reloads the active shell without rebuilding the provider. A gate proves changes during an admitted operation apply only to the next event.

- [ ] T003 [US1] Update `src/CShells.Nuplane/Internal/NuplaneRefreshCoordinator.cs` to read `CurrentValue` once after null/cancellation/eligibility checks, validate the enum, copy all four policy values before epochs/registry/awaits, and use only that private immutable snapshot through refresh, reload, and result reporting.
- [ ] T004 [US1] Add deterministic tests in `tests/CShells.Tests/Integration/Nuplane/NuplaneRefreshCoordinatorTests.cs` for false-to-true automatic reload across deliveries and for a gated refresh where mutating the captured settings object and replacing `OnReloadResults` does not alter the current operation but the next delivery sees the new values.
- [ ] T005 [US1] Add or extend a DI integration test in `tests/CShells.Tests/Integration/Nuplane/NuplaneCompositionTests.cs` to prove configuration reload reaches the adapter through standard options binding, while retaining fluent and dependency-aware configuration coverage.

## Phase 4: User Story 2 - Preserve pending work and fail bad policy before side effects (P2)

**Goal**: Disabled or paused behavior does not lose already-recorded work, and configuration errors cannot partially mutate coordinator state.

**Independent Test**: Gate a freshness/reload attempt, toggle enabled/reload policy, and assert existing epochs survive and can be retried without unnecessary catalog work. Use monitor, registry, and catalog counters to prove empty/canceled/error paths remain side-effect free.

- [ ] T006 [US2] Extend `tests/CShells.Tests/Integration/Nuplane/NuplaneRefreshCoordinatorTests.cs` to prove disabling observer work records no new epochs while a build consumes prior freshness without reading options, and automatic-reload-off retains pending promotion work for a later enabled delivery without rescanning fresh catalog state.
- [ ] T007 [US2] Add monitor-read, catalog, and registry counter tests in `tests/CShells.Tests/Integration/Nuplane/NuplaneRefreshCoordinatorTests.cs` for failed-only, empty, pre-canceled, undefined refresh-trigger, throwing monitor, and options-validation failures; follow rejected invalid reads with a valid unchanged delivery/build and prove no work was recorded.
- [ ] T008 [US2] Update `src/CShells.Nuplane/README.md` to document next-eligible-delivery settings, per-operation callback capture, preserved pending work, standard configuration paths, and direct `IOptions<NuplaneIntegrationOptions>`-only replacement as unsupported.

## Phase 5: Verification and Publication Qualification

**Purpose**: Prove the behavioral contract, catch restoration of the stale snapshot, and validate the actual delivered packages.

- [ ] T009 Run the focused Nuplane tests, full `tests/CShells.Tests/CShells.Tests.csproj` suite, and Release build of `src/CShells.Nuplane/CShells.Nuplane.csproj` for net8.0/net9.0/net10.0; then perform a compiled frozen-options mutation that fails the dynamic options test and verify byte-identical restoration passes, recording exact-head evidence in `specs/023-live-nuplane-options/quickstart.md`.
- [ ] T010 After normal owner-repository PR merge and main checks, audit all ten public package archives and run an outside-checkout, fresh-cache PackageReference-only dynamic-options consumer on actual .NET 8, 9, and 10 runtimes; record source/package identity, cache source, hashes, loaded DLL assets, exact commands, and scenario results in `specs/023-live-nuplane-options/quickstart.md` and the owning issue.

## Dependencies and Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001 provides the shared test monitor.
- **Foundational (Phase 2)**: T002 depends on T001 and updates existing direct coordinator tests for the internal constructor change.
- **US1 (Phase 3)**: T003 and T004 depend on T002; T005 validates real options binding and existing DI composition.
- **US2 (Phase 4)**: T006 and T007 depend on T003; T008 may be reviewed alongside implementation but must describe the delivered behavior.
- **Verification (Phase 5)**: T009 follows implementation and documentation. T010 requires passing local/hosted gates, normal main publication, and an audited actual public package set.

### User Story Dependencies

- **US1 (P1)**: Independent after the shared test-monitor setup; delivers live options on subsequent eligible callbacks.
- **US2 (P2)**: Uses the same captured policy and existing epochs from US1; its tests can be completed after T003.

## Implementation Strategy

The MVP is US1: replace startup-only option consumption with a one-read immutable per-delivery policy and prove later deliveries observe reloaded settings. Complete US2 before acceptance so disabled, pending, and invalid-setting behavior remains safe. Tests must use deterministic gates and bounded cleanup. Public package qualification is a separate final acceptance gate; preview or private-package results do not close it.
