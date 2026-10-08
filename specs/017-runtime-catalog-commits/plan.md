# Implementation Plan: Runtime Feature Catalog Commit Notifications

**Branch**: `017-runtime-catalog-commits` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/017-runtime-catalog-commits/spec.md`

## Summary

Add an optional `IRuntimeFeatureCatalogCommitSource` capability that publishes the exact detailed `RuntimeFeatureCatalogSnapshot` after each successful commit. The stock public accessor forwards the capability from its backing catalog while existing custom `IRuntimeFeatureCatalog` implementations remain unchanged. The catalog serializes snapshot publication and queue insertion with refreshes, then drains ordered notifications outside the refresh semaphore with subscriber exception isolation.

## Technical Context

**Language/Version**: C# 14; source projects target `net8.0;net9.0;net10.0`
**Primary Dependencies**: Existing Microsoft.Extensions.DependencyInjection and Microsoft.Extensions.Logging abstractions
**Storage**: N/A
**Testing**: xUnit 2.x, focused catalog unit tests in `tests/CShells.Tests/Unit/Features/`
**Target Platform**: .NET 8, 9, and 10
**Project Type**: .NET library and abstractions package
**Performance Goals**: No subscriber callbacks while holding the catalog refresh semaphore; one dispatch drainer at a time
**Constraints**: No required changes to `IRuntimeFeatureCatalog`; no separate DI alias that can diverge from a host override; no replay; exact detailed snapshot payload; commits and notifications stay ordered.
**Scale/Scope**: One optional abstraction contract, catalog queue/dispatcher, stock-accessor capability, tests, and package documentation; no new dependencies.

## Constitution Check

- **I. Abstraction-first**: PASS. The optional public contract and its snapshot payload belong in `CShells.Abstractions`; implementation stays in `CShells`.
- **III. Modern C#**: PASS. Nullable annotations and XML documentation will cover every public contract member.
- **IV. Explicit errors**: PASS. Subscriber failures are logged individually and do not fail a commit or suppress later handlers.
- **V. Test coverage**: PASS. Deterministic tests cover initial/repeated/subsequent commits, failures, cancellation, reentrancy, concurrency, ordering, unsubscription, and legacy implementations.
- **VI. Simplicity**: PASS. Use one short private queue gate and one FIFO queue; do not add background services or user-configurable dispatch machinery.
- **VII. Lifecycle and concurrency**: PASS. Refresh remains serialized by the existing semaphore; publication uses explicit volatile visibility; callbacks run after both commit and queue gates have been released.

## Phase 0: Research

See [research.md](research.md). The source catalog currently commits while holding one `SemaphoreSlim`; the implementation will make lock-free snapshot reads explicit and add a small ordered queue whose enqueue/dequeue handoff is atomic under a private gate.

## Phase 1: Design & Contracts

- [data-model.md](data-model.md) defines commit generations and queued notifications.
- [contracts/IRuntimeFeatureCatalogCommitSource.md](contracts/IRuntimeFeatureCatalogCommitSource.md) defines the optional source event and late-subscription semantics.
- [quickstart.md](quickstart.md) provides deterministic validation scenarios.
- The agent-context update workflow was run; no new technology entry is warranted for an existing Abstractions contract and existing runtime dependencies.

## Project Structure

```text
src/CShells.Abstractions/Features/IRuntimeFeatureCatalogCommitSource.cs
src/CShells/Features/RuntimeFeatureCatalog.cs
src/CShells/Features/RuntimeFeatureCatalogAccessor.cs
tests/CShells.Tests/Unit/Features/RuntimeFeatureCatalogCommitSourceTests.cs
src/CShells.Abstractions/README.md
src/CShells/README.md
specs/017-runtime-catalog-commits/
```

**Structure Decision**: Put the optional event contract and immutable event arguments beside `IRuntimeFeatureCatalog` in the abstractions package. Keep queueing in the internal runtime catalog, expose it through the already-registered stock accessor, and do not add an `IRuntimeFeatureCatalogCommitSource` DI registration.

## Complexity Tracking

No constitution violations or new third-party dependencies are required.
