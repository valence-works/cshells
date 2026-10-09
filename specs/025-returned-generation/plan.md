# Implementation Plan: Returned Generation Diagnostics

**Branch**: `025-returned-generation` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `specs/025-returned-generation/spec.md` (CShells #167)

## Summary

Add a nullable scalar returned-generation value to the transient activation attempt and immutable target snapshot. Populate it from the descriptor of the successful shell returned by the runner's own registry call, for built-in and custom registries. Keep `VerifiedGeneration` semantics unchanged, leave existing public constructors unchanged, and ensure external satisfaction does not fabricate a returned value.

## Technical Context

**Language/Version**: C# 14 / .NET 10 source language; production libraries target .NET 8, 9, and 10.  
**Primary Dependencies**: Existing CShells and Microsoft.Extensions dependencies; no new package dependency.  
**Storage**: None.  
**Testing**: xUnit in `tests/CShells.Tests`; focused and full library suites, the end-to-end suite, compatibility probes, and production builds for every library target framework.  
**Target Platform**: .NET library consumers.  
**Project Type**: Multi-project .NET library.  
**Performance Goals**: No added work beyond copying one nullable scalar into existing result/snapshot objects.  
**Constraints**: Preserve all existing public constructor signatures and current settlement, retry, cancellation, and external-reconciliation behavior. Do not retain shell/provider references.  
**Scale/Scope**: Two result types, the activation attempt recording/projection path, focused tests, and API XML documentation; no package dependency or host-policy changes.

## Constitution Check

- **I. Abstraction-first architecture**: The metadata is part of public attempt and snapshot result contracts, so expose it in `CShells.Abstractions`; keep its value a scalar and add no framework dependency.
- **III. Modern C# style**: Use nullable annotations, documented public members, and existing record/property conventions.
- **IV. Explicit error handling**: Do not change error handling; failed/canceled calls carry no returned generation.
- **V. Test coverage**: Required. Add deterministic xUnit coverage for built-in/custom success, non-success outcomes, external satisfaction, constructor compatibility, and terminal-history stability.
- **VI. Simplicity and minimalism**: Add one property to each existing result type and thread the value through the existing record/update path. No new service, interface, option, or state machine.
- **VII. Lifecycle and concurrency contracts**: The returned descriptor generation is sampled from the result already held by the current attempt. Store only its scalar value; later registry changes must not rewrite the terminal snapshot.

**Gate result**: Pass. This is additive diagnostic data, with no change to lifecycle decisions or ownership. Constructor binary compatibility, lifecycle behavior, and public-package behavior are explicit verification gates.

## Design Decisions

1. **Keep constructor ABI stable.** Add an init-only nullable `long` scalar property to each existing result type rather than adding a constructor parameter. `ShellActivationAttempt` and `ShellActivationAttemptState` retain their current constructor signatures. The scalar type follows the existing `VerifiedGeneration` public type; descriptor `int` generations widen losslessly.
2. **Capture only an accepted own return.** Set the attempt value only after `GetOrActivateAsync` returns and the existing current-identity/settlement check yields `Succeeded`. Read the generation from that returned `IShell.Descriptor`; do not perform another registry lookup to derive it.
3. **Preserve the diagnostic distinction.** Continue populating `VerifiedGeneration` exactly as today: built-in settled shells on own success and eligible built-in external settlement; custom success remains null. External settlement sets no `ReturnedGeneration`, because this run did not successfully return that shell.
4. **Keep attempt attribution separate from external settlement.** Project `ReturnedGeneration` from the accepted attempt into the existing state snapshot. External settlement alone leaves it null. If an already in-flight attempt completes successfully after external satisfaction, record that attempt's returned generation while preserving the existing external status and verified-generation semantics. Once the successful-return association is recorded, later registry changes do not replace it.
5. **Do not change lifecycle outcomes.** `NotCurrent`, activation failures, and activation calls canceled before successful return carry no returned generation. A cancellation arriving after the call has successfully returned does not change that call's outcome.

## Project Structure

### Documentation (this feature)

```text
specs/025-returned-generation/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/returned-generation.md
└── tasks.md
```

### Source and tests

```text
src/CShells.Abstractions/Hosting/ShellActivationAttempt.cs
src/CShells.Abstractions/Hosting/ShellActivationAttemptState.cs
src/CShells/Hosting/ShellActivationRun.cs
tests/CShells.Tests/Integration/Hosting/ShellActivationRunnerTests.cs
```

**Structure Decision**: Extend the current public result models in the abstractions assembly and populate them at the existing attempt-recording boundary in the implementation assembly. Keep focused behavior tests alongside the activation-runner integration tests. No new projects or packages.

## Validation Strategy

- Run the focused activation-runner tests, the full `CShells.Tests` suite, and `CShells.Tests.EndToEnd`; preserve the existing assertion that successful custom-registry results have null `VerifiedGeneration`.
- Add deterministic coverage for returned generation on built-in and custom own-success paths; null behavior on `NotCurrent`, failed and canceled calls; external satisfaction; and unchanged terminal association after a later generation appears.
- Assert exact existing constructor parameter signatures by reflection, and run a consumer compiled against pre-change abstractions with the candidate abstractions binary.
- Build `src/CShells/CShells.csproj` without a framework filter; its project reference must build `CShells.Abstractions` for `net8.0`, `net9.0`, and `net10.0`.
- Perform a compiled reversible mutation that removes the successful returned-generation assignment: the causal returned-generation test must fail; restore byte-identical source and require the focused suite to pass.
- Before completion, pass exact-source review, current-head hosted PR checks, resulting-main checks, normal preview-family publication, complete family archive identity audit, and a fresh-cache PackageReference-only consumer on .NET 8, 9, and 10.

## Complexity Tracking

No constitution violations or complexity exceptions.
