# Feature Specification: Nuplane Observer Failure Diagnostics

**Feature Branch**: `024-nuplane-failure-diagnostics`
**Created**: 2026-10-09
**Status**: Draft
**Input**: Add generic observer operation failure diagnostics to the existing CShells.Nuplane coordinator without changing its public API or swallowing failures.

## User Scenarios & Testing

### User Story 1 - Diagnose package reconciliation failures (Priority: P1)

As a host maintainer using the optional Nuplane integration, I want an operational error record when an admitted package reconciliation operation fails, while the same failure still reaches the caller and remains retryable, so that I can diagnose it without changing Nuplane's observer-delivery behavior.

**Why this priority**: Foundation's existing Nuplane adoption relies on an Error record containing the original exception. The published adapter currently allows the exception to reach Nuplane's observer dispatcher, which isolates it and continues delivery, but emits only a generic Warning without the exception payload.

**Independent Test**: Register the adapter through dependency injection with a capturing logger, inject refresh, registry/reload, and result-callback failures, and verify one Error record containing the original exception and reconciliation correlation, unchanged exception propagation, and retry on a later eligible delivery. Then dispatch through Nuplane's real observer dispatcher and verify that a following observer still runs.

**Acceptance Scenarios**:

1. **Given** an enabled adapter and an eligible reconciliation delivery, **When** catalog refresh fails, **Then** one Error record contains that same exception and the reconciliation's correlation ID, the same exception instance propagates, and the pending freshness can be retried on a later eligible delivery.
2. **Given** an enabled adapter with automatic reload, **When** active-registry access or the reload call throws, **Then** one Error record contains the same exception and the reconciliation's correlation ID, the same exception instance propagates, and pending reload work remains retryable.
3. **Given** an enabled adapter with automatic reload and a configured result callback, **When** that callback throws, **Then** one Error record contains the same exception and the reconciliation's correlation ID, the same exception instance propagates, and pending reload work remains retryable.
4. **Given** a caller-requested cancellation during an admitted operation, **When** the operation propagates an `OperationCanceledException` while the callback's token is requested, **Then** it is not recorded as an Error and pending work is not acknowledged.
5. **Given** a fatal runtime exception during an admitted operation, **When** it propagates, **Then** it is not recorded as an Error.
6. **Given** a reconciliation callback delivered through Nuplane's observer dispatcher, **When** the adapter logs and rethrows an ordinary operation failure, **Then** the dispatcher retains its own warning behavior and continues to the next observer.
7. **Given** no logging provider is registered, **When** an ordinary operation failure occurs, **Then** the adapter remains usable and propagates the original exception unchanged.

### Edge Cases

- An eligible delivery is a completed reconciliation callback with one or more applied packages or a committed removal, and observer work is enabled.
- A failed-only or otherwise ineligible completion does no coordinator work and emits no new Error record.
- An options-read or refresh-trigger validation failure occurs before observer work is admitted; it retains its existing propagation and no-work behavior without an adapter Error record.
- An `OperationCanceledException` is an ordinary operation failure when the callback's cancellation token was not requested; it is Error-logged and rethrown unchanged. A requested token alone does not suppress logging for a different exception type.
- Caller-requested cancellation and these fatal exceptions are excluded from Error logging: `OutOfMemoryException`, `StackOverflowException`, `AccessViolationException`, `AppDomainUnloadedException`, and `BadImageFormatException`.
- A build-time freshness failure remains outside this observer-diagnostics boundary and retains its existing propagation behavior.
- Returned per-shell reload results with an `Error` remain result data for the configured callback; they are not themselves a thrown coordinator operation failure.
- A logger is optional. Its absence must not alter observer behavior.

## Requirements

### Functional Requirements

- **FR-001**: Diagnostics MUST apply only after a completed reconciliation delivery with applied packages or a committed removal has passed existing option validation with observer work enabled and entered observer work.
- **FR-002**: An ordinary nonfatal exception thrown by catalog refresh, active-registry access, reload invocation, or the configured reload-results callback MUST produce exactly one Error record for that failed observer operation.
- **FR-003**: The Error record MUST carry the original exception object and the structured correlation ID supplied for the reconciliation; it MUST NOT replace the exception with a wrapper or formatted-message-only diagnostic.
- **FR-004**: The observer MUST rethrow the original exception instance unchanged after recording it. It MUST preserve existing gate release, epoch acknowledgement, and retry behavior.
- **FR-005**: An `OperationCanceledException` MUST propagate without an Error record only when the callback's cancellation token is requested. Fatal runtime exceptions MUST propagate without an Error record. An uncanceled `OperationCanceledException` MUST follow the ordinary failure behavior.
- **FR-006**: The integration MUST work when no logging provider is present; no new public configuration or logging API is introduced.
- **FR-007**: The adapter's Error record MUST supplement, not replace, Nuplane's existing observer-isolation warning. The failure MUST remain available to Nuplane's dispatcher so subsequent observers continue to run.
- **FR-008**: Build-time catalog freshness and returned per-shell `ReloadResult.Error` values MUST retain their existing semantics and remain outside the new exception-diagnostic behavior.

### Key Entities

- **Observer operation failure**: An exception thrown by admitted reconciliation work in catalog refresh, active-registry access, reload invocation, or result-callback execution.
- **Reconciliation correlation**: The `CorrelationId` supplied with the reconciliation delivery; it is emitted as a structured logging value without adding coordinator state.
- **Error record**: One Error-level logging event that carries the original exception object and reconciliation correlation.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Each injected ordinary failure from refresh, registry/reload, and result-callback execution yields exactly one Error record containing the exact thrown exception instance and `PackageChangeSet.CorrelationId` value.
- **SC-002**: Each injected ordinary failure still propagates as the exact original exception instance, and a subsequent eligible delivery can retry the retained work.
- **SC-003**: Requested cancellation and each listed fatal exception produce zero adapter Error records while retaining propagation.
- **SC-004**: Through the real Nuplane observer dispatcher, the adapter Error is observed, Nuplane's existing warning is preserved, and a following observer is invoked.
- **SC-005**: The optional adapter remains constructible and propagates failures when the host has no logging provider.
