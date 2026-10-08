# Research: Nuplane Observer Failure Diagnostics

## Decisions

### 1. Use the existing typed-logger convention and optional fallback

- **Decision**: Give the private `NuplaneRefreshCoordinator` an optional `ILogger<NuplaneRefreshCoordinator>` and use `NullLogger<NuplaneRefreshCoordinator>.Instance` when absent. Resolve the optional logger in the existing `CoordinatorHolder` service factory and pass it explicitly to the coordinator. Add a direct `Microsoft.Extensions.Logging.Abstractions` package reference to `CShells.Nuplane.csproj`.
- **Rationale**: Other CShells services use this optional typed-logger pattern. The adapter is optional and is also composed in service collections without logging. `Directory.Packages.props` already pins `Microsoft.Extensions.Logging.Abstractions` for all three target-framework groups (9.0.11 for net8/net9 and 10.0.0 for net10), so no new central version is required.
- **Alternatives considered**: A required logger would break no-provider composition; adding an adapter-specific logger abstraction would add a layer with no current consumer need.

### 2. Keep the diagnostic boundary after existing admission and policy validation

- **Decision**: Preserve argument guards, the caller-token precheck, ineligible-completion return, and option/trigger validation before the new Error boundary. Do not change `BeginAsync`. For admitted observer work, log one Error with the original exception and `changeSet.CorrelationId`, then use a bare rethrow so the same instance reaches Nuplane's dispatcher or a direct caller. Do not change gate `finally` blocks or acknowledge epochs on failure.
- **Rationale**: `PackageChangeSet.CorrelationId` is Nuplane's documented reconciliation-cycle identifier, and `ObserverEventDispatcher` already places that same value on its warning. The accepted Foundation contract requires the original exception and this correlation; the coordinator's internal freshness epoch is not the reconciliation ID.
- **Alternatives considered**: Logging inside `BeginAsync` would extend diagnostics to a distinct build-time operation. Wrapping or swallowing the exception would break upstream exception-identity tests and observer isolation semantics.

### 3. Preserve the established cancellation and fatal-exception filter

- **Decision**: Do not Error-log an `OperationCanceledException` only when the callback token is requested. Treat an uncanceled `OperationCanceledException` as an ordinary failure. Do not Error-log `OutOfMemoryException`, `StackOverflowException`, `AccessViolationException`, `AppDomainUnloadedException`, or `BadImageFormatException`. Other admitted nonfatal exceptions are logged and rethrown.
- **Rationale**: This is the concrete exclusion behavior used by Foundation's existing observer integration. It distinguishes caller cancellation from an operation that merely happens to throw an `OperationCanceledException`.
- **Alternatives considered**: Excluding every `OperationCanceledException` would hide non-cancellation failures; excluding every exception whenever the token is signaled would also hide unrelated faults.

### 4. Verify both the coordinator boundary and Nuplane's real fan-out

- **Decision**: Use focused coordinator tests for refresh, registry/reload, and callback failures, and a DI-backed test that resolves the public Nuplane `ObserverEventDispatcher` / `ReconciliationLogger` path with a following observer. Add `Nuplane` `0.0.11-preview.99` only to the net10 test project with a central pin; do not add it to the shipping adapter graph. Keep a no-logging-provider composition test.
- **Rationale**: The actual published dispatcher catches observer exceptions, records its existing Warning with correlation and message only, and continues through later observers. An independently reviewed package probe showed the current adapter has no Error entry and the dispatcher warning lacks the exception object. The fix must add the adapter Error without replacing this established fan-out behavior.
- **Alternatives considered**: Direct observer invocation alone cannot prove the actual dispatcher continues after the adapter's rethrow. A test-only dispatch shim cannot prove the public runtime path.

## Source Findings

- The coordinator currently owns refresh/reload epoch gates and preserves failed work for later eligible deliveries. Keep this state machine unchanged.
- `CShellsNuplaneBuilderExtensions` creates the private coordinator manually inside the `CoordinatorHolder` factory; this is the point to pass a logger resolved with optional `GetService`.
- The test project already references the adapter project. It currently sees only Nuplane abstractions transitively; the real `ObserverEventDispatcher` is in the Nuplane runtime package. The external public-package probe provides a concrete registration/dispatch pattern. No new test project or public contract is needed.
- Returned `ReloadResult.Error` values flow to the existing options callback and retain their current meaning; only thrown operation failures enter this diagnostics boundary.
