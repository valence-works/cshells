# Data Model: Nuplane Observer Failure Diagnostics

This change adds no persisted data, public model, coordinator state, or new identifier.

| Existing input | Use in diagnostics | Boundary |
|---|---|---|
| `PackageChangeSet.CorrelationId` | Structured `CorrelationId` value on the adapter's Error record | Copied from the current reconciliation delivery; Nuplane uses the same value on its observer warning. |
| Thrown `Exception` | The exception argument on one Error record and the exception rethrown from the observer | The object is not wrapped, translated, or replaced. |
| Observer operation | Fixed structured `Operation` value `OnPackagesReconciledAsync` | Identifies the event boundary; the original exception remains the evidence for the specific failing call. No stage tracker is added. |
| Callback cancellation token | Determines whether a thrown `OperationCanceledException` represents caller-requested cancellation | Suppression applies only when the token is requested and the thrown exception is `OperationCanceledException`. |

Failure classification is limited to the existing observer callback. An `OperationCanceledException` with an uncanceled token is an ordinary failure. `OutOfMemoryException`, `StackOverflowException`, `AccessViolationException`, `AppDomainUnloadedException`, and `BadImageFormatException` remain unlogged fatal exceptions. A returned `ReloadResult.Error` is data for the configured result callback, not a thrown operation failure.
