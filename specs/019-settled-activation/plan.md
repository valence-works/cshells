# Implementation Plan: Settled Shell Activation Results

**Branch**: `019-settled-activation` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/019-settled-activation/spec.md`

## Summary

Activation publishes a candidate before activation participants finish committing so that lifecycle/routing participants can resolve the exact candidate. The fast path in `GetOrActivateAsync` currently treats any published shell as complete. Add an internal immutable-after-commit marker to concrete `Shell`; the lock-free path returns only a marked shell, and an unmarked shell makes the caller wait on the existing per-name semaphore. Under the semaphore, recheck the active generation after the original activation operation has committed or rolled back. Do not change early visibility APIs or add a public capability or runner.

## Technical Context

**Language/Version**: C# 14; source projects target net8.0, net9.0, and net10.0
**Primary Dependencies**: Existing CShells lifecycle and Microsoft.Extensions abstractions; no new packages
**Storage**: None
**Testing**: xUnit 2.x in `tests/CShells.Tests/`
**Target Platform**: .NET 8, .NET 9, and .NET 10
**Project Type**: Multi-target .NET library
**Performance Goals**: Preserve the lock-free fast path for settled shells; do not serialize requests for different shell names
**Constraints**: No public API change; preserve early candidate visibility for GetActive/GetAll; do not duplicate same-name generation builds; cancellation affects only the waiting caller
**Scale/Scope**: Internal state marker, registry fast-path/recheck, deterministic lifecycle integration tests, concise docs

## Constitution Check

- Principle I: No public interface is added; the marker is internal to concrete framework `Shell`.
- Principle III: Uses the existing C# 14/.NET targets and volatile visibility for cross-thread state.
- Principle IV: An uncommitted shell under the acquired name semaphore is treated as an explicit invariant violation.
- Principle V: Add deterministic xUnit coverage for publication, commit/rollback, cancellation, reload, and no-participant behavior.
- Principle VI: Reuse `NameSlot.Semaphore`; do not add a queue, event, coordinator, or public runner API.
- Principle VII: Keep synchronization async-safe and retain existing per-name serialization.

No constitution exception is required.

## Project Structure

```text
src/CShells/Lifecycle/
├── Shell.cs
└── ShellRegistry.cs

tests/CShells.Tests/Integration/Lifecycle/
└── ShellRegistryActivationSettlementTests.cs
```

**Structure Decision**: Keep the state on the concrete internal `Shell`, and update its sole activation orchestrator `ShellRegistry`. Add one focused integration test file beside existing activation and reload tests. The build lease work in #147 remains separate and unchanged.

## Concurrency and Failure Decisions

- The committed bit starts false and only changes to true after commit fan-out, completion callbacks, and the final active-slot eligibility check. It never resets for that generation.
- `GetActive` and `GetAll` continue to expose a published candidate during participant commit.
- `GetOrActivateAsync` returns a fast-path shell only when committed; otherwise it awaits the same name semaphore using its own cancellation token.
- Once inside the semaphore, return the current committed shell. If an active shell is still uncommitted, throw a clear invariant exception rather than returning it or starting another build.
- If commit rolls back, the waiter observes the restored committed shell. If no shell remains, it performs the ordinary serialized activation flow.
- Mark commitment before success logging, because logging is observational and may throw after activation has settled.
- Treat `Complete` failures as best-effort cleanup; guard their error logging too, so a throwing logger cannot skip the final eligibility check or leave the generation permanently provisional.
- Same-name activation/reload/unregister reentry from activation participant callbacks remains unsupported; those callbacks execute inside the existing name transaction.

## Verification

Use `TaskCompletionSource` gates around participant `Commit`, with bounded awaits and finally blocks that release gates and await worker completion. Record outcomes outside swallowed observer callbacks. Test both initial activation and reload, successful commit and rollback, cancellation, candidate removal before commitment, logging failure, same-name stampede safety, and the ordinary no-participant path. Run focused activation/reload/routing tests and a multi-target CShells build; root owns the complete `CShells.Tests` regression.
