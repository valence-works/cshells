# Feature Specification: Settled Shell Activation Results

**Feature Branch**: `019-settled-activation`
**Created**: 2026-10-08
**Status**: Draft
**Input**: User description: "Wait for committed activation before returning GetOrActivate results"

## User Scenarios & Testing

### User Story 1 - Receive only a settled shell from activation (Priority: P1)

A host asks the registry for a shell while that shell is being activated. The host receives a successful result only after activation has either committed or rolled back. Lifecycle observers may still inspect the candidate while activation is in progress.

**Why this priority**: A provisional generation must not escape as a successful result if activation later fails; otherwise callers may use a generation that the registry is about to reject.

**Independent Test**: Pause activation after the candidate becomes visible to lifecycle observers. Verify a concurrent activation request remains pending, then release activation and verify it receives the committed candidate or the restored/recovered generation after failure.

**Acceptance Scenarios**:

1. **Given** a candidate is visible to lifecycle observers and its activation commit is pending, **When** another caller requests that shell, **Then** the caller does not complete with the candidate before commit settles.
2. **Given** the candidate's commit succeeds, **When** the pending request resumes, **Then** it receives the committed candidate.
3. **Given** the candidate's commit fails, **When** rollback completes, **Then** the pending request receives the restored committed shell or follows the ordinary activation path when none remains.
4. **Given** a committed shell is serving while a replacement is still being composed, **When** a caller requests the shell, **Then** it may receive the existing committed generation.
5. **Given** a caller is waiting for activation settlement, **When** that caller's cancellation token is cancelled, **Then** only that wait is cancelled and the original activation continues.

## Edge Cases

- The candidate is drained or removed before activation reaches its final commit point; it must not become a successful activation result.
- A success logger throws after activation work is complete; the committed state must remain consistent before the name lock is released.
- A completion callback or its error logger throws; later completion callbacks still run and activation still reaches a settled result.
- Concurrent callers request the same shell; they must not start duplicate activation work.
- Direct active-shell enumeration can observe a candidate during commit for routing and lifecycle coordination.

## Requirements

### Functional Requirements

- **FR-001**: The registry MUST return a generation from activation requests only after that generation has completed activation successfully.
- **FR-002**: While a candidate's activation is unsettled, a concurrent activation request for the same shell MUST wait for the existing serialized operation to settle.
- **FR-003**: After successful activation, waiting callers MUST receive the new generation.
- **FR-004**: After failed activation, waiting callers MUST receive the restored serving generation or continue the existing serialized activation behavior when no serving generation remains.
- **FR-005**: A waiting caller's cancellation MUST NOT cancel or corrupt the activation operation it was waiting for.
- **FR-006**: Lifecycle and active-shell observers MUST retain access to the candidate during activation so existing routing and participant behavior remains possible.
- **FR-007**: The registry MUST preserve single-generation serialization for concurrent requests and ordinary activation behavior when no participant delays settlement.

## Key Entities

- **Shell generation**: A named serving instance with a unique generation number whose activation progresses from candidate visibility to committed or rejected state.
- **Activation request**: A caller's request to obtain an active shell, which completes with a committed generation or the existing activation error/cancellation behavior.

## Success Criteria

### Measurable Outcomes

- **SC-001**: In deterministic tests, no activation request completes successfully with a generation before its commit succeeds.
- **SC-002**: Every blocked activation request completes with the committed candidate or the settled rollback/recovery result after the activation operation finishes.
- **SC-003**: Cancelling a waiting request leaves the in-progress activation able to complete or fail independently.
- **SC-004**: Existing routing and lifecycle behavior can still identify the candidate while its activation is pending.
