# Tasks: Nuplane Observer Failure Diagnostics

**Input**: Design documents from `specs/024-nuplane-failure-diagnostics/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

## Phase 1: Setup

**Purpose**: Declare the implementation dependency and the actual-dispatcher test dependency.

- [X] T001 Add `Microsoft.Extensions.Logging.Abstractions` to `src/CShells.Nuplane/CShells.Nuplane.csproj`; keep using the existing target-framework-specific central pins in `Directory.Packages.props`.
- [X] T002 Add the `Nuplane` runtime package to `tests/CShells.Tests/CShells.Tests.csproj` and add its test-only central version pin `0.0.11-preview.99` in `Directory.Packages.props`; do not add it to the shipping adapter project.

## Phase 2: Foundational

**Purpose**: Provide shared structured log capture for the focused test cases.

- [X] T003 [P] Add a reusable capturing `ILoggerProvider` in `tests/CShells.Tests/Integration/Nuplane/NuplaneTestLoggerProvider.cs` that retains level, exception instance, structured state, and category.

## Phase 3: User Story 1 - Diagnose package reconciliation failures (Priority: P1)

**Goal**: Operators can identify an admitted Nuplane reconciliation failure from one Error record while callers and Nuplane retain existing exception and retry behavior.

**Independent Test**: Focused tests prove identity-preserving logging and retry for each thrown observer operation; direct cancellation/fatal exclusions; logger-optional DI; actual Nuplane dispatcher warning and continuation.

### Tests for User Story 1

- [X] T004 [US1] Add coordinator tests in `tests/CShells.Tests/Integration/Nuplane/NuplaneRefreshCoordinatorTests.cs` for one Error with `PackageChangeSet.CorrelationId`, original exception identity, and retained retry after refresh failure; also assert Begin-only refresh failure remains unlogged and unchanged.
- [X] T005 [US1] Add logger classification tests in `tests/CShells.Tests/Integration/Nuplane/NuplaneRefreshCoordinatorTests.cs` and validation-boundary assertions in `tests/CShells.Tests/Integration/Nuplane/NuplaneLiveOptionsTests.cs` for requested-token cancellation, uncanceled `OperationCanceledException`, each of `OutOfMemoryException`, `StackOverflowException`, `AccessViolationException`, `AppDomainUnloadedException`, and `BadImageFormatException`, plus option-read/invalid-trigger failures that precede observer admission and emit no adapter Error.
- [X] T006 [P] [US1] Add registry/reload and result-callback failure, exception-identity, and pending-retry assertions in `tests/CShells.Tests/Integration/Nuplane/NuplaneReloadResultsTests.cs`; assert returned `ReloadResult.Error` remains callback data and produces no new adapter Error.
- [X] T007 [P] [US1] Add DI logger-factory wiring and no-`AddLogging` propagation coverage in `tests/CShells.Tests/Integration/Nuplane/NuplaneCompositionTests.cs`.
- [X] T008 [P] [US1] Add `tests/CShells.Tests/Integration/Nuplane/NuplaneObserverDiagnosticsTests.cs` using the real `ObserverEventDispatcher` and `ReconciliationLogger`; assert the adapter Error carries the exception and correlation, Nuplane's Warning remains, and the following observer runs and can retry.

### Implementation for User Story 1

- [X] T009 [US1] Add optional `ILogger<NuplaneRefreshCoordinator>` injection with `NullLogger` fallback and a narrow admitted-observer catch/log/rethrow boundary in `src/CShells.Nuplane/Internal/NuplaneRefreshCoordinator.cs`; include fixed `OnPackagesReconciledAsync` operation and reconciliation correlation fields, exclude only requested-token cancellation and the five specified fatal types, and preserve gates, epochs, retries, `BeginAsync`, and returned-result semantics.
- [X] T010 [US1] Pass the optional typed logger through the existing `CoordinatorHolder` manual factory in `src/CShells.Nuplane/CShellsNuplaneBuilderExtensions.cs`, using optional service resolution so hosts without logging remain valid.
- [X] T011 [US1] Document the optional logger behavior and unchanged propagation boundary in `src/CShells.Nuplane/README.md` without adding public options or API.

## Phase 4: Verification and package qualification

**Purpose**: Prove the implementation against the focused source tests and supported package surfaces.

- [X] T012 Run focused Nuplane tests and the full `tests/CShells.Tests/CShells.Tests.csproj` Release suite; record exact commands and results in the issue's evidence before closure.
- [X] T013 Build `src/CShells.Nuplane/CShells.Nuplane.csproj` in Release for `net8.0`, `net9.0`, and `net10.0`; retain exact command results.
- [X] T014 In a disposable copy of `src/CShells.Nuplane/Internal/NuplaneRefreshCoordinator.cs`, remove the diagnostics path and compile/run the focused diagnostic tests to prove they detect the regression; restore the intended file and verify no mutation residue.
- [X] T015 Open the normal organization PR after root and independent review, pass required PR CI/review gates, merge under repository policy, and verify the `.github/workflows/publish.yml` main-branch gate and owner-produced preview publication before treating package artifacts as acceptance evidence.
- [X] T016 After the owner-published corrective preview is available, audit all ten public CShells package archives against successful workflow metadata and feed bytes, then run the PackageReference-only diagnostic consumer on actual .NET 8, .NET 9, and .NET 10 runtimes using `public-consumer-plan.md` in the modular-hosting evidence artifact workspace. Keep this proof distinct from the accepted pre-correction counterexample; do not manually publish or promote a stable release as part of this task.

## Dependencies and Execution Order

- Setup T001–T002 precedes the tests and implementation.
- T003 is shared test infrastructure. T004–T008 can be drafted independently by file after setup, but the verification sequence starts only after T009–T010 implement the expected behavior.
- T009 and T010 depend on the direct adapter package reference and logger contract. T011 follows the implementation.
- T012–T014 require the complete source change. T015 is the normal merge/main-preview gate. T016 requires owner-published corrective package artifacts after that gate and is not source-only acceptance.

## Implementation Strategy

Deliver the narrow adapter correction first: direct logging dependency, focused failure tests, one private logger boundary, and the explicit DI factory update. Verify tests and all three library target frameworks before repository review. Complete the required organization PR/main gate, then the actual-public archive and runtime consumer gate before issue closure. Do not manually publish packages, change stable pins, or conflate the old public counterexample with corrective-package proof.
