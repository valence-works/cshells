# Implementation Plan: Nuplane Observer Failure Diagnostics

**Branch**: `024-nuplane-failure-diagnostics` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/024-nuplane-failure-diagnostics/spec.md`

## Summary

Make admitted Nuplane observer operation failures visible as one structured Error record carrying the original exception and reconciliation correlation, then preserve existing exception propagation and pending-work retry. Add an optional typed logger to the private coordinator, use `NullLogger` when no provider is registered, and resolve the logger in the existing explicit coordinator-holder factory. Leave build-time freshness, public APIs, coordinator state, and Nuplane's observer isolation unchanged.

## Technical Context

**Language/Version**: C# 14 / .NET 10 SDK; source library targets `net8.0;net9.0;net10.0`
**Primary Dependencies**: Existing Nuplane abstractions, Microsoft.Extensions.Options, and a direct `Microsoft.Extensions.Logging.Abstractions` dependency; test-only reference to Nuplane's real observer-dispatcher package
**Storage**: N/A
**Testing**: xUnit integration tests; source project Release builds for all three target frameworks; public PackageReference consumer qualification for all three runtime versions
**Target Platform**: .NET 8, .NET 9, and .NET 10
**Project Type**: Multi-target .NET library and integration-test project
**Performance Goals**: No additional work on ineligible callbacks or `BeginAsync`; one Error logging call only on an admitted operation failure
**Constraints**: Preserve the original exception instance, existing epoch acknowledgements/retry, cancellation and fatal-exception exclusions, logger-optional composition, and observer continuation through Nuplane's dispatcher
**Scale/Scope**: The existing private coordinator and its package/test qualification only; no new public API, public options, timers, state machine, host policy, or package-store mutation

## Constitution Check

**Pre-design gate**

- **I. Abstraction-First Architecture**: Pass. This adds no public consumer contract and no abstraction project. The logging dependency belongs only to the optional implementation package.
- **III. Modern C# Style**: Pass. Keep nullable annotations, file-scoped namespaces, idiomatic `var`, and existing source multi-targets.
- **IV. Explicit Error Handling**: Pass. Failures are Error-logged and rethrown unchanged; no coordinator swallowing or exception wrapping is introduced.
- **V. Test Coverage**: Pass. Add focused xUnit cases for the three failure sources, exclusions, DI, and the actual observer dispatcher.
- **VI. Simplicity & Minimalism**: Pass. Reuse the existing coordinator, holder, logging abstractions, and options; do not add a logger abstraction or helper layer.
- **VII. Lifecycle & Concurrency Contracts**: Pass. Keep the existing `finally` gate release and epoch acknowledgement paths; do not alter Nuplane fan-out isolation. Nuplane's dispatcher remains the boundary that catches the rethrown observer exception and continues to later observers.

No constitution exception or unresolved gate remains.

**Post-design gate**: The design adds only a private logger dependency and a catch/log/rethrow boundary around already-admitted observer work. It introduces no locks, async work, state transitions, public contract, or provider ownership. All gates remain satisfied.

## Design and Research

- [Research decisions](research.md) records source-backed choices and rejected alternatives.
- [Failure diagnostic contract](contracts/observer-diagnostic-contract.md) records the externally observable log/exception boundary without defining a new API.
- [Data model](data-model.md) describes the existing reconciliation correlation; no durable entities or coordinator state are added.
- [Quickstart and validation](quickstart.md) gives the composition shape and focused verification sequence.

## Project Structure

```text
Directory.Packages.props                              # central test-only Nuplane runtime pin at 0.0.11-preview.99
src/CShells.Nuplane/
├── CShells.Nuplane.csproj                         # direct logging abstractions dependency
├── CShellsNuplaneBuilderExtensions.cs             # manual holder factory supplies optional logger
├── Internal/
│   └── NuplaneRefreshCoordinator.cs               # failure classification, one Error, unchanged rethrow
└── README.md                                      # optional logging behavior and diagnostic boundary

tests/CShells.Tests/CShells.Tests.csproj            # test-only Nuplane dispatcher package reference
tests/CShells.Tests/Integration/Nuplane/
├── NuplaneRefreshCoordinatorTests.cs              # refresh failure, identity, exclusion, retry
├── NuplaneReloadResultsTests.cs                   # registry/reload and callback failures, identity, retry
├── NuplaneCompositionTests.cs                      # DI logger wiring and no-provider composition
├── NuplaneObserverDiagnosticsTests.cs              # actual Nuplane dispatcher + following observer
└── NuplaneTestLoggerProvider.cs                     # shared structured log capture for integration tests
```

**Structure Decision**: Keep production changes in the existing optional adapter. Add a trailing optional `ILogger<NuplaneRefreshCoordinator>? logger = null` constructor parameter and use `NullLogger<NuplaneRefreshCoordinator>.Instance` when absent; pass `serviceProvider.GetService<ILogger<NuplaneRefreshCoordinator>>()` through the existing holder factory. Add Nuplane `0.0.11-preview.99` only to the net10 integration-test project, with a central test pin, so acceptance can exercise the actual `ObserverEventDispatcher` and `ReconciliationLogger`; do not add a runtime dependency from the adapter package or use a test-only dispatch shim.

## Complexity Tracking

No constitution violations or additional architectural components are planned.
