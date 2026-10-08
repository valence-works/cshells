# Observer Diagnostic Contract

This document describes an observable behavior of the existing optional adapter. It introduces no public type, method, option, or Nuplane API.

## Ordinary admitted-operation failure

For a nonfatal exception thrown by catalog refresh, active-registry access, reload invocation, or the configured reload-results callback during an enabled, eligible reconciliation delivery, the adapter emits exactly one `Error` record before propagating the failure.

The record carries:

- the exact original exception object as the logging exception;
- structured `CorrelationId`, copied from `PackageChangeSet.CorrelationId`;
- structured `Operation`, identifying the observer callback as `OnPackagesReconciledAsync`.

The adapter rethrows that same exception instance. It does not format and substitute the exception, wrap it, swallow it, or clear pending work. Nuplane's actual `ObserverEventDispatcher` remains responsible for its existing Warning and continuation to later observers.

## Exclusions and unchanged behavior

- Do not Error-log a thrown `OperationCanceledException` when the callback token is requested. An uncanceled `OperationCanceledException` is an ordinary operation failure.
- Do not Error-log `OutOfMemoryException`, `StackOverflowException`, `AccessViolationException`, `AppDomainUnloadedException`, or `BadImageFormatException`.
- Do not extend the new diagnostic boundary to build-time freshness in `BeginAsync`.
- A returned `ReloadResult.Error` is not a thrown failure and is not newly Error-logged; preserve the existing result callback and pending-reload behavior.
- If the host has no logging provider, construction and operation remain valid through the null logger fallback.
- Exception-preserving diagnostics assume registered logging providers do not throw from `ILogger.Log`; logger-provider failures are outside this adapter contract.

No particular log message wording or event ID is a public compatibility promise. The exception object, structured correlation, observer-operation name, Error level, one-record count, and exception propagation are the contract.
