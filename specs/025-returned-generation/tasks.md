# Tasks: Returned Generation Diagnostics

**Input**: Design documents from `specs/025-returned-generation/`  
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/returned-generation.md`

## Phase 1: User Story 1 - Identify the generation returned by an activation attempt (Priority: P1)

**Goal**: Expose the scalar generation of each successful current own return, including custom registry returns, without changing verified-generation or activation behavior.

**Independent Test**: Run the focused activation-runner suite. Successful built-in/custom returns report the exact returned generation; stale, failed, and call-canceled outcomes do not; external-only satisfaction remains returned-generation-null; a successful late return during external satisfaction is recorded without changing existing external status or verified-generation; the terminal value survives later registry changes.

### Tests for User Story 1

- [x] T001 [US1] Extend `tests/CShells.Tests/Integration/Hosting/ShellActivationRunnerTests.cs`: assert successful built-in and custom attempts/snapshots report the exact returned descriptor generation while existing custom `VerifiedGeneration == null` assertions remain unchanged; assert stale `NotCurrent`, failed, and canceled calls report no returned generation.
- [x] T002 [US1] Extend the existing external-satisfaction tests in `tests/CShells.Tests/Integration/Hosting/ShellActivationRunnerTests.cs`: prove external-only satisfaction leaves returned-generation null; cover both branches of `ExternalSatisfactionIsTerminalAgainstLateResultAndLaterDrain`, recording a successful late own return but not a stale late return while preserving external status and verified-generation; assert a later generation cannot overwrite a completed returned-generation association.
- [x] T003 [US1] Add reflection assertions in `tests/CShells.Tests/Integration/Hosting/ShellActivationRunnerTests.cs` for the exact existing public constructor parameter signatures of `ShellActivationAttempt` and `ShellActivationAttemptState`; also exercise those constructors and assert returned-generation defaults to null.

### Implementation for User Story 1

- [x] T004 [US1] Add documented nullable `long` init-only `ReturnedGeneration` metadata without changing the public constructor in `src/CShells.Abstractions/Hosting/ShellActivationAttempt.cs`.
- [x] T005 [US1] Add documented nullable `long` init-only `ReturnedGeneration` metadata without changing the public constructor in `src/CShells.Abstractions/Hosting/ShellActivationAttemptState.cs`.
- [x] T006 [US1] Populate attempt returned-generation from the exact shell returned by the accepted successful `GetOrActivateAsync` call and project it through attempt recording while preserving external terminal status and all `VerifiedGeneration` behavior in `src/CShells/Hosting/ShellActivationRun.cs`.
- [x] T007 [US1] Update public API documentation to distinguish returned-generation from verified-generation in `src/CShells.Abstractions/README.md` and `src/CShells/README.md`, following the repository's existing API documentation placement.
- [x] T008 [US1] Run the focused `ShellActivationRunnerTests` suite and build `src/CShells/CShells.csproj` without a framework filter so its project reference builds `CShells.Abstractions` for `net8.0`, `net9.0`, and `net10.0`; retain raw logs and test results.
- [x] T009 [US1] Run `dotnet test tests/CShells.Tests/CShells.Tests.csproj` and `dotnet test tests/CShells.Tests.EndToEnd/CShells.Tests.EndToEnd.csproj`; retain raw logs and test results.
- [x] T010 [US1] Compile a small compatibility consumer against the pre-change `CShells.Abstractions` binary from the base commit, then run the unchanged consumer with the candidate `CShells.Abstractions` binary substituted; prove both existing activation-result constructors bind and execute, and retain source/package provenance and logs under `artifacts/modular-hosting-2500/cshells-167/` in the session-owned artifact workspace.
- [x] T011 [US1] Perform the compiled reversible mutation from `specs/025-returned-generation/quickstart.md`: remove only the successful returned-generation assignment, prove the causal returned-generation test fails, restore byte-identical source, rebuild, and rerun the focused suite successfully; retain mutation and restoration evidence.
- [x] T012 [US1] Complete an independent exact-source review of the candidate diff and test evidence before opening an organization pull request; record the reviewed commit and findings under `artifacts/modular-hosting-2500/cshells-167/` in the session-owned artifact workspace.
- [ ] T013 [US1] Open the normal organization pull request after local gates and exact-source review pass; require all current-head hosted checks and applicable review gates to pass before merge.
- [ ] T014 [US1] After normal merge, verify resulting-main checks and the normal preview-package publication; audit the complete published CShells family for source commit, package/version metadata, dependency versions, TFMs, and archive/DLL identity under `artifacts/modular-hosting-2500/cshells-167/` in the session-owned artifact workspace.
- [ ] T015 [US1] Build and run a fresh-cache PackageReference-only consumer against the exact published preview package family on .NET 8, 9, and 10; verify successful built-in/custom returned-generation values, `NotCurrent`/failure/cancellation null values, external-only and in-flight external-satisfaction behavior, later-generation stability, and existing verified-generation semantics; retain loaded-assembly/archive/cache provenance and logs under `artifacts/modular-hosting-2500/cshells-167/` in the session-owned artifact workspace.
- [ ] T016 [US1] Post the required issue evidence for implementation, review, merge, resulting-main checks, and public-package qualification to CShells #167, then release the issue claim only after the task is complete.

## Dependencies & Execution Order

### Phase Dependencies

- T001-T003 specify the focused behavior and ABI proof; implementation tasks T004-T007 follow. No mandatory pre-implementation failing-test run is prescribed.
- Focused verification and the all-target production build (T008) follow the implementation; full library and end-to-end suites (T009) follow focused verification.
- Binary compatibility proof (T010) and causal mutation/restoration proof (T011) follow the implementation and use isolated evidence artifacts.
- Exact-source review (T012) precedes the organization pull request and hosted checks (T013).
- Resulting-main verification and normal preview publication (T014) precede the fresh-cache public package consumer on all three runtimes (T015).
- Issue evidence and claim release (T016) are last; do not claim completion before public-package qualification.

### User Story Dependencies

- **User Story 1 (P1)**: Standalone; no other story or product change is required.

## Implementation Strategy

1. Add deterministic regression assertions to existing activation-runner tests, preserving every existing `VerifiedGeneration` assertion. The task order does not require a pre-implementation failing run.
2. Add the two scalar properties and populate/project them at the existing accepted-return/attempt-recording boundaries.
3. Document the distinction, run focused and full local suites, all production target builds, constructor/binary compatibility proof, then compiled mutation and restored pass.
4. Complete exact-source review, hosted PR/resulting-main gates, normal preview publication, and the fresh-cache three-runtime consumer before closing the issue.

## Validation Notes

- Do not change public constructor parameter lists, lifecycle settlement, retry policy, cancellation, external reconciliation, or readiness behavior.
- Test assertions must distinguish the external target's existing `VerifiedGeneration` from a later successful in-flight call's `ReturnedGeneration`.
- No new project, dependency, package pin, or generated-map update is expected.
