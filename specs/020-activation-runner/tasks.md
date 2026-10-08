# Tasks: Opt-in Shell Activation Runner

**Input**: Design documents from `specs/020-activation-runner/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/runner-api.md`

## Phase 1: User Story 1 - Host explicitly starts and observes shell activation (P1)

**Goal**: Hosts can opt into one generic run that owns serial initial activation, optional retries, snapshots, and bounded stop behavior without changing default CShells startup.

**Independent Test**: Use FakeTimeProvider and gated registry/commit participants to prove the initial-pass boundary, per-target retry decisions, committed-state recognition, cancellation/stop ownership, snapshot privacy, and opt-in root-only DI.

### Setup

- [x] T001 Pin `Microsoft.Extensions.TimeProvider.Testing` 10.8.0 in `Directory.Packages.props`, reference it only from `tests/CShells.Tests/CShells.Tests.csproj`, then perform a real restore and review any generated lockfile.

### Public contracts

- [x] T002 Add documented attempt, target-state, outcome/status and retry-decision models in `src/CShells.Abstractions/Hosting/`.
- [x] T003 Add documented runner, run-handle, retry-policy delegate, and attempt-observer contracts in `src/CShells.Abstractions/Hosting/`.

### Runtime and registration

- [x] T004 Implement the opt-in `IServiceCollection.AddShellActivationRunner()` extension with TryAdd semantics in `src/CShells/DependencyInjection/ShellActivationRunnerServiceCollectionExtensions.cs`.
- [x] T005 Implement a root-only runner that resolves the registry and optional TimeProvider at root service resolution in `src/CShells/Hosting/ShellActivationRunner.cs`.
- [x] T006 Add immutable target state, serial initial pass, InitialPass boundary, cancellation ownership, safe snapshots, and snapshot reconciliation in `src/CShells/Hosting/ShellActivationRun.cs`.
- [x] T007 Add post-initial retry workers, validated policy decisions/deadlines, observer isolation, and bounded idempotent Stop to `src/CShells/Hosting/ShellActivationRun.cs`.
- [x] T008 Exclude `IShellActivationRunner` from child shell service copies in `src/CShells/Hosting/DefaultShellServiceExclusionProvider.cs`.

### Tests

- [x] T009 Verify opt-in registration, registration order, TryAdd/custom-runner preservation, AddCShells-only laziness, and child-provider exclusion in `tests/CShells.Tests/Integration/Hosting/ShellActivationRunnerTests.cs`.
- [x] T010 Verify synchronous input validation, case-insensitive stable deduplication, empty input, immediate handle return, serial initial order, and continuation after ordinary target failure in `tests/CShells.Tests/Integration/Hosting/ShellActivationRunnerTests.cs`.
- [x] T011 Verify retry policy input, retry-after timing only after InitialPass, FakeTimeProvider advancement, stop/no-policy behavior, and actual scheduler `NextAttemptAt` in `tests/CShells.Tests/Integration/Hosting/ShellActivationRunnerTests.cs`.
- [x] T012 Verify activation failure, not-current without a synthetic exception, sanitized snapshots, raw transient callback classification, throwing observer, snapshot reentry, and target-local policy errors in `tests/CShells.Tests/Integration/Hosting/ShellActivationRunnerTests.cs`.
- [x] T013 Verify committed default-shell external satisfaction, provisional/replaced shell rejection, post-return removal, terminal satisfaction after later drain, and custom registry current identity in `tests/CShells.Tests/Integration/Hosting/ShellActivationRunnerTests.cs`.
- [x] T014 Verify startup cancellation, Stop during delay/activation, stubborn activation bounded join and eventual fault observation, repeated Stop after completion, and linked-token-source disposal only after owned work finishes in `tests/CShells.Tests/Integration/Hosting/ShellActivationRunnerTests.cs`.

## Phase 2: Polish and verification

- [x] T015 Document explicit registration, lazy default behavior, snapshots, retry policy, and bounded Stop in `src/CShells/README.md`.
- [x] T016 Review APIs/runtime/tests for privacy, cancellation races, task ownership, callbacks outside gates, root-only registration, and unnecessary abstractions; run the focused runner and existing activation/reload tests plus the net8/net9/net10 CShells build.
- [x] T017 Mutate external reconciliation to accept a provisional active candidate or move retry scheduling before the InitialPass boundary; prove the causal regression fails, restore the implementation, and prove it passes in `tests/CShells.Tests/Integration/Hosting/ShellActivationRunnerTests.cs`.
- [x] T018 Validate `quickstart.md`, confirm all tasks are complete, and recheck Issue #149 comments/open PRs before the local commit.

## Dependencies and execution order

- T001 precedes test compilation and deterministic time tests. T002/T003 define public inputs before runtime implementation. T004–T008 compose the runtime and DI slice. T009–T014 verify independent contract areas against the integrated runner. T015–T017 are the final review and proof gate; T018 precedes commit.
- No task adds hosted startup behavior, host adapters, Elsa/Nuplane policy, or public stable-state capability.
