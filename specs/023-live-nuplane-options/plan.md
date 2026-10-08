# Implementation Plan: Apply Live Nuplane Integration Options

**Branch**: `023-live-nuplane-options` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/023-live-nuplane-options/spec.md`

## Summary

Make the optional CShells.Nuplane coordinator consume current supported option values for each eligible package-reconciliation completion. Capture and copy one small immutable per-delivery policy before epoch mutation or asynchronous work, then carry those copied values through freshness, automatic reload, and reload-result reporting. Preserve current epochs, build participation, registration aliases, eligibility, reentry, and settlement behavior.

## Technical Context

**Language/Version**: C# 14; source packages target net8.0, net9.0, net10.0

**Primary Dependencies**: Existing `Microsoft.Extensions.Options`, `Microsoft.Extensions.Configuration`, and CShells/Nuplane abstractions; no new package

**Storage**: Existing in-memory refresh and promotion epochs; no new persisted state

**Testing**: xUnit in `tests/CShells.Tests`, net10.0; external public-package consumer on actual net8/net9/net10 after publication

**Target Platform**: Existing supported .NET runtimes

**Project Type**: Multi-package library; this work is confined to `src/CShells.Nuplane`

**Performance Goals**: No new listener, background timer, queue, or per-delivery allocation beyond a small immutable value snapshot; no separate performance target

**Constraints**: Snapshot current options once after callback validation/cancellation/eligibility checks and before epoch mutation or awaits; do not promise an atomic multi-property read against concurrent mutation of one options object; preserve current package-observer ordering and root/child ownership

**Scale/Scope**: One internal adapter policy change, deterministic tests, adapter documentation, and required publication qualification; no CShells-core, Nuplane-core, Foundation host, or stable-package changes

## Constitution Check

- **I. Abstraction-first**: No consumer-extensibility API is added. Existing public `NuplaneIntegrationOptions` remains the contract; implementation remains in the optional adapter.
- **II. Feature modularity**: No feature composition, assembly discovery, or shell policy changes are introduced.
- **III. Modern C#**: Preserve nullable, language, package-target, and repository style conventions.
- **IV. Explicit errors**: An undefined `RefreshTrigger` gets an actionable configuration error before state/registry work. Monitor and options-validation failures propagate before that work without a new wrapping-error contract.
- **V. Test coverage**: Add deterministic adapter regressions for changes across deliveries, changes during an admitted operation, pending/deferred work, invalid settings, empty/canceled callbacks, and existing configuration paths. The repository requires tests; it does not declare test-first/TDD cadence for this feature.
- **VI. Simplicity**: Use one immutable private policy value and one `IOptionsMonitor` read per eligible callback. Add no public abstraction, second configuration source, background listener, timer, general settings framework, or new package dependency.
- **VII. Lifecycle and concurrency**: Keep epoch locks/gates and async sequencing as-is. Options are copied before awaits; no user callback or async operation is introduced while holding a new lock.

Pre-design check passes. Recheck after design: no principle violation or exception is needed.

## Design Decisions

1. Resolve `IOptionsMonitor<NuplaneIntegrationOptions>` in the root-owned coordinator and update the coordinator factory in `CShellsNuplaneBuilderExtensions.cs` to supply that monitor. Keep existing `Configure(Action<NuplaneIntegrationOptions>)`, standard bind, and dependency-aware `Configure<TDependency>` paths.
2. For an eligible, non-canceled callback, read `CurrentValue` exactly once and synchronously copy `Enabled`, `RefreshTrigger`, `AutoReload`, and `OnReloadResults` into a private immutable value before mutating epochs or resolving the registry. Empty/failed-only and already-canceled callbacks return or cancel before reading options.
3. Validate that the copied refresh trigger is a defined enum member before any epoch or registry work and report an actionable configuration error for an invalid value. Let options-monitor and options-validation failures propagate before side effects; do not add last-known-good fallback or promise to wrap arbitrary provider errors.
4. Use the copied values throughout that callback, including callback invocation and result interpretation. Changes after capture apply to later deliveries and do not cancel or rewrite current work.
5. Keep existing epoch state authoritative. Disabled observer work creates no new epochs and leaves old ones intact. Automatic reload off may refresh catalog state but does not attempt or acknowledge pending reload work. A later enabled eligible delivery retries pending reload work without rescanning a fresh catalog unless a newer source change requires it.
6. Keep `BeginAsync` independent of observer options and retain existing catalog refresh-before-catalog-read ordering. Do not add an options-change listener or settings dependency to the build participant.
7. Treat direct replacement of `IOptions<NuplaneIntegrationOptions>` alone as unsupported. Standard options configuration or explicit `IOptionsMonitor<NuplaneIntegrationOptions>` registration is supported; do not add dual-source precedence or compatibility shims.

See [research.md](research.md), [data-model.md](data-model.md), and [contracts/options-policy.md](contracts/options-policy.md) for source evidence and exact policy boundaries.

## Design and Test Cadence

The source repository's active constitution requires tests but does not impose a test-first cadence. Implement deterministic regression cases with the adapter change, then run the focused suite. Gates must be controlled with `TaskCompletionSource` and bounded joins, not sleeps. Existing tests that instantiate the internal coordinator with `IOptions<T>` need a small shared test monitor or service-provider resolution through the real options system; do not retain production `IOptions<T>` as a compatibility path.

The critical snapshot proof holds catalog refresh after policy capture, changes values on the held options object and replaces the callback, then releases refresh. The admitted delivery must use its copied policy/callback; a subsequent eligible delivery must use the new values. Additional gates prove a later enabled delivery retries retained reload work without rescanning. Failed-only and pre-canceled callbacks must make zero monitor reads. Invalid enum/current-options failures must leave epoch/catalog/registry counters unchanged.

## Project Structure

```text
specs/023-live-nuplane-options/
  spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
  checklists/requirements.md
  contracts/options-policy.md
src/CShells.Nuplane/
  Internal/NuplaneRefreshCoordinator.cs
  CShellsNuplaneBuilderExtensions.cs
  README.md
tests/CShells.Tests/Integration/Nuplane/
  NuplaneRefreshCoordinatorTests.cs
  NuplaneCompositionTests.cs
  NuplaneLiveOptionsTests.cs
  NuplaneCoordinatorTestHarness.cs or a narrowly scoped options-monitor helper
```

**Structure Decision**: Keep the implementation in the existing optional adapter and use existing Nuplane integration test fixtures. Avoid new projects, public options abstractions, core changes, or duplicate test harnesses.

## Verification

Run, once on the final implementation head:

1. Focused `NuplaneRefreshCoordinatorTests`, `NuplaneCompositionTests`, `NuplaneLiveOptionsTests`, and `NuplaneReloadResultsTests`.
2. Full `tests/CShells.Tests/CShells.Tests.csproj` suite.
3. Release builds of `src/CShells.Nuplane/CShells.Nuplane.csproj` for net8.0, net9.0, and net10.0.
4. A compiled freeze-options mutation that restores the old captured-settings behavior; the live-options regression must fail under the mutation and pass after byte-identical restoration.
5. Required owner-repository PR CI and main checks, then a complete ten-package actual-public-Feedz archive audit and a fresh-cache PackageReference-only options-change consumer on actual net8/net9/net10 runtimes. Record exact version, source commit, archive hashes, loaded DLL identities, commands, and outputs. Do not count local or private package preflight as public package evidence.

Final stable release and Foundation host adoption/pins/locks are separate gates owned outside this feature.

## Complexity Tracking

No constitution exceptions or added complexity are proposed.
