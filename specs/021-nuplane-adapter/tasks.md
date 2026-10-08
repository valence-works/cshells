# Tasks: Optional Nuplane Feature Discovery and Deferred Catalog Freshness

**Input**: Design documents from `/specs/021-nuplane-adapter/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/CShells.Nuplane.md](contracts/CShells.Nuplane.md), [quickstart.md](quickstart.md)

**Tests**: Tests are required by the acceptance criteria and are organized before their implementation tasks for each story.

**Organization**: Tasks are grouped by user story. The shared test host uses real CShells DI and registry pipeline with a fake Nuplane public catalog/observer dispatch so provider, catalog, and actual feature construction are tested together.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the optional package to the repository and make Nuplane's published abstractions resolvable.

- [X] T001 Create the `src/CShells.Nuplane/CShells.Nuplane.csproj` package project targeting `net8.0`, `net9.0`, and `net10.0`, with only CShells abstractions/core, Nuplane abstractions/loading abstractions, and required Microsoft options/DI abstraction dependencies.
- [X] T002 [P] Add central `Nuplane.Abstractions` and `Nuplane.Loading.Abstractions` pins at `0.0.11-preview.99` in `Directory.Packages.props`, and map `Nuplane*` packages to the Nuplane Feedz source in `NuGet.Config`.
- [X] T003 Add `src/CShells.Nuplane/CShells.Nuplane.csproj` to `CShells.sln` and add it as the tenth explicit project in `.github/workflows/publish.yml`.
- [X] T004 [P] Add the adapter project reference and reusable real-DI test host/fakes in `tests/CShells.Tests/CShells.Tests.csproj` and `tests/CShells.Tests/Integration/Nuplane/NuplaneTestHost.cs`, including deterministic disposal and visible teardown failures.

**Checkpoint**: The optional package restores from the mapped Nuplane feed, appears in the solution and canonical pack list, and the test host can compose a root service provider and real shell registry.

## Phase 2: User Story 1 - Discover features from loaded Nuplane packages (Priority: P1)

**Goal**: Provide an explicit CShells provider backed only by Nuplane's already-loaded package assemblies.

**Independent Test**: With real DI and a fake `IPackageAssemblyCatalog`, verify catalog-order `Assembly` results, ignore `AssemblyReferences`, honor cancellation and empty catalogs, and confirm installing the adapter does not select it until the host calls its composition extension.

### Tests for User Story 1

- [X] T005 [P] [US1] Add provider contract tests in `tests/CShells.Tests/Integration/Nuplane/NuplaneFeatureAssemblyProviderTests.cs` for loaded-assembly ordering, ignored file references, unavailable/empty catalogs, and cancellation.
- [X] T006 [P] [US1] Add explicit-provider-selection, existing-provider compatibility, and real CShells shell-promotion feature-construction tests in `tests/CShells.Tests/Integration/Nuplane/NuplaneCompositionTests.cs`; use a fake public Nuplane catalog here and leave real autoload/reconciliation proof to the root-owned package-consumer gate.

### Implementation for User Story 1

- [X] T007 [US1] Implement `NuplaneFeatureAssemblyProvider` in `src/CShells.Nuplane/NuplaneFeatureAssemblyProvider.cs` by flattening only `PackageAssemblies.Assemblies` in catalog order and propagating cancellation.
- [X] T008 [US1] Add `CShellsBuilder.WithNuplaneFeatureDiscovery` in `src/CShells.Nuplane/CShellsNuplaneBuilderExtensions.cs` and select the provider through CShells' existing `WithAssemblyProvider<NuplaneFeatureAssemblyProvider>()` path; leave existing providers unchanged unless the host calls this method.

**Checkpoint**: An opted-in host builds a shell containing a feature from a catalog-reported loaded assembly; an unconfigured host retains its previous provider behavior.

## Phase 3: User Story 2 - Refresh features before a requested shell build (Priority: P1)

**Goal**: Retain catalog freshness while no shell is active and refresh before CShells initializes or reads the catalog for a real build.

**Independent Test**: Report an eligible package addition/removal with no active shell; assert zero scan, activation, and reload; request a build and verify refresh precedes feature selection. Verify the last package's feature disappears, failed promotion does not cause redundant refresh, and under `ChangedOrPending` a later unchanged callback neither rescans nor reloads the first shell promoted from the deferred refresh.

### Tests for User Story 2

- [X] T009 [P] [US2] Add deferred freshness, failure-before-promotion, last-package-removal-to-zero, and cold `AutoReload=true` regression scenarios in `tests/CShells.Tests/Integration/Nuplane/NuplaneRefreshCoordinatorTests.cs` using the real registry pipeline.
- [X] T010 [P] [US2] Add root/shell alias identity, both service-resolution orders, lazy `Func<IShellRegistry>` resolution, adapter descriptor order after a previously registered observer, and unrelated-observer identity/disposal tests in `tests/CShells.Tests/Integration/Nuplane/NuplaneCompositionTests.cs`; treat this as adapter DI proof, not proof of Nuplane autoload behavior.

### Implementation for User Story 2

- [X] T011 [US2] Implement the private root-owned `CoordinatorHolder` and non-disposable `NuplaneRefreshCoordinator` in `src/CShells.Nuplane/Internal/CoordinatorHolder.cs` and `src/CShells.Nuplane/Internal/NuplaneRefreshCoordinator.cs` with deferred `Func<IShellRegistry>` resolution and a no-op build lease in `src/CShells.Nuplane/Internal/NoOpBuildLease.cs`.
- [X] T012 [US2] Register one observer factory after Nuplane autoload and existing Nuplane observer registrations, alias the root coordinator to `IShellGenerationBuildParticipant`, and share only the private holder in `src/CShells.Nuplane/CShellsNuplaneBuilderExtensions.cs`.
- [X] T013 [US2] Implement catalog refresh in `IShellGenerationBuildParticipant.BeginAsync` before runtime catalog initialization/read in `src/CShells.Nuplane/Internal/NuplaneRefreshCoordinator.cs`; capture and acknowledge only the successful epoch, preserve newer events, and skip scanning/reloading when no active shell exists.

**Checkpoint**: Cold-start package work remains pending without activation or scanning, the next real build uses a fresh catalog, and successful final-package removal yields zero features on the next catalog read/build.

## Phase 4: User Story 3 - Preserve each host's refresh and reload policy (Priority: P1)

**Goal**: Expose independent observer enablement, refresh-trigger selection, and automatic reload while retaining generic and host profile defaults.

**Independent Test**: Exercise generic defaults, Foundation.Host profile (`EveryEligibleCompletion` plus reload), Workbench profile (`ChangedOrPending` without reload), disabled observer, and reload-off behavior against identical eligible completions.

### Tests for User Story 3

- [X] T014 [P] [US3] Add option-default, profile, unchanged/changed/pending trigger, observer-disabled, and reload-off tests in `tests/CShells.Tests/Integration/Nuplane/NuplaneRefreshCoordinatorTests.cs`.

### Implementation for User Story 3

- [X] T015 [US3] Add `NuplaneRefreshTrigger` and `NuplaneIntegrationOptions` in `src/CShells.Nuplane/NuplaneRefreshTrigger.cs` and `src/CShells.Nuplane/NuplaneIntegrationOptions.cs` with generic defaults enabled, changed-or-pending, and auto reload disabled.
- [X] T016 [US3] Register options through standard `IOptions<NuplaneIntegrationOptions>` configuration in `src/CShells.Nuplane/CShellsNuplaneBuilderExtensions.cs`, preserving dependency-aware `Configure<TDependency>` callbacks and the explicit provider selection.
- [X] T017 [US3] Implement `Enabled`, `ChangedOrPending`, `EveryEligibleCompletion`, and independent `AutoReload` decisions in `src/CShells.Nuplane/Internal/NuplaneRefreshCoordinator.cs`; keep provider initial discovery available when observer work is disabled.

**Checkpoint**: All three controls behave independently; refresh can acknowledge catalog freshness with reload disabled, and both host profiles match their documented refresh/reload counts.

## Phase 5: User Story 4 - Retry failed work without coupling unrelated observers (Priority: P2)

**Goal**: Preserve independent catalog/reload pending state across failures, cancellation, callback failures, and events received during refresh.

**Independent Test**: Inject refresh and registry failures, cancellations, partial per-shell errors, a nested host refusal exception, a throwing result callback, and an unrelated failing observer. Verify captured epochs remain pending as specified, later observers still run, callback receives raw stable results, and reload re-entry does not hold the refresh gate.

### Tests for User Story 4

- [X] T018 [P] [US4] Add refresh failure/cancellation, newer-event-during-refresh, and refresh-gate release-before-reentrant-reload tests in `tests/CShells.Tests/Integration/Nuplane/NuplaneRefreshCoordinatorTests.cs`.
- [X] T019 [P] [US4] Add returned partial-error, thrown registry exception, callback raw-result identity/read-only snapshot, callback failure/cancellation, reload retry, and exception-preservation tests in `tests/CShells.Tests/Integration/Nuplane/NuplaneReloadResultsTests.cs`; assert callback errors escape the adapter unchanged, with later-observer isolation covered by the root-owned real Nuplane consumer gate.

### Implementation for User Story 4

- [X] T020 [US4] Protect catalog request/commit and reload request/commit epochs in `src/CShells.Nuplane/Internal/NuplaneRefreshCoordinator.cs`; serialize refresh and reload separately, always release the refresh gate before `ReloadActiveAsync`, and retain newer source work.
- [X] T021 [US4] Inspect every `ReloadResult.Error` and add `OnReloadResults` as `Func<IReadOnlyList<ReloadResult>, CancellationToken, ValueTask>?` in `src/CShells.Nuplane/NuplaneIntegrationOptions.cs`; pass a copied read-only snapshot with original partial results and exception chains, acknowledge only after all results and the callback succeed, and propagate thrown errors without masking them.
- [X] T022 [US4] Keep failed/cancelled reload work pending for a later eligible completion and keep retries callback-driven without timer or quiet-cycle loops in `src/CShells.Nuplane/Internal/NuplaneRefreshCoordinator.cs`.

**Checkpoint**: Catalog freshness and reload completion advance independently; a later eligible callback can retry reload without rescanning when policy allows; observer failures do not couple unrelated observers.

## Phase 6: Polish, package validation, and release qualification

**Purpose**: Document the public composition shape, verify all target frameworks and package contents, and keep published-consumer proof as an explicit gate.

- [X] T023 [P] Add the composition, option profiles, raw reload callback, and deferred-freshness examples to `src/CShells.Nuplane/README.md`, matching the actual `AddNuplane(nuplane => ...)` callback API and required namespaces.
- [X] T024 Build `src/CShells.Nuplane/CShells.Nuplane.csproj` for `net8.0`, `net9.0`, and `net10.0`, then run the focused Nuplane integration suite from `tests/CShells.Tests/CShells.Tests.csproj` on its configured `net10.0` test target through the build-slot wrapper.
- [X] T025 Pack `src/CShells.Nuplane/CShells.Nuplane.csproj` with a private preview version, inspect its nuspec for CShells and Nuplane abstractions, Microsoft options/DI abstractions, the repository-wide `JetBrains.Annotations` dependency, and no Elsa/Nuplane runtime package; verify `.github/workflows/publish.yml` contains exactly ten package entries.
- [X] T026 Run causal mutation/revert bites against `NuplaneFeatureAssemblyProvider` loaded-assembly selection and captured-epoch acknowledgement in `src/CShells.Nuplane/NuplaneFeatureAssemblyProvider.cs` and `src/CShells.Nuplane/Internal/NuplaneRefreshCoordinator.cs`; record that the focused tests fail under each mutation and pass after restoration.
- [ ] T027 Root-owned acceptance gate: after the adapter package is actually published, restore an outside-checkout consumer from the published CShells feed plus actual Nuplane `0.0.11-preview.99` runtime/loading feeds, compile the documented `AddNuplane(nuplane => nuplane.AutoloadPackages())` quickstart shape, and exercise real reconciliation/loading, package-to-catalog discovery, actual feature construction, final-removal-to-zero, and later-observer delivery after an adapter callback failure without project references or private reflection; save proof under `/Users/sipke/.codex-workspaces/artifacts/modular-hosting-2500/cshells-nuplane-156/`. Source builds and fake catalog/observer tests alone do not complete this release gate.

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** precedes all stories because the adapter project, feed mapping, solution, pack list, and reusable test host are shared infrastructure.
- **User Story 1 (Phase 2)** can start after Setup and establishes the optional provider.
- **User Story 2 (Phase 3)** builds on the provider and explicit composition from User Story 1.
- **User Story 3 (Phase 4)** configures the coordinator from User Story 2.
- **User Story 4 (Phase 5)** hardens the state and reload behavior from User Stories 2 and 3.
- **Polish (Phase 6)** follows the behavior stories; published-consumer qualification waits for package publication.

### User Story Dependencies

- **US1** has no story dependency and is the provider MVP.
- **US2** depends on US1 because the cold build must consume the provider's loaded assemblies.
- **US3** depends on US2 because options govern observer and participant coordination.
- **US4** depends on US2 and US3 because it tests separate refresh/reload state and host-selected retry behavior.

### Parallel Opportunities

- In Setup, T002 and T004 touch separate files and can run alongside T001; T003 follows project creation.
- In US1, T005 and T006 are independent tests; implementation T007 and T008 can proceed after their red tests are established.
- In US2, T009 and T010 cover separate test files and may be authored in parallel; coordinator and DI changes remain sequenced to avoid conflicting edits to `CShellsNuplaneBuilderExtensions.cs`.
- In US4, T018, T019, and T020 are separate focused test additions and may be authored in parallel before implementation tasks.

## Implementation Strategy

1. Complete Setup and the US1 provider MVP; verify opt-in discovery and actual feature construction.
2. Add the US2 deferred-refresh coordinator and prove cold build, last-removal-to-zero, and the no-gratuitous-first-shell-reload regression.
3. Add the US3 option profiles, then US4 failure/retry and host-result callback behavior.
4. Complete local target-framework, focused test, package-content, and causal mutation/revert checks.
5. Keep the actual published outside-checkout consumer as a release gate after CShells.Nuplane is published; update Nuplane references to final `0.0.11` only in the release-owner follow-up.

## Notes

- Never select assemblies from `AssemblyReferences`, load packages, or add Elsa dependencies.
- The private holder and coordinator stay non-disposable and managed-only; only the holder crosses into shell providers.
- Do not edit Foundation pins/locks, Nuplane core, product stores, protected-main, or unload/protection policy in this feature branch.
- The `CShells.Nuplane` package remains optional and is not auto-selected merely because it is installed.
