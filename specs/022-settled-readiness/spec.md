# Feature Specification: Observe Settled Active Generations

**Feature Branch**: `022-settled-readiness`  
**Created**: 2026-10-08  
**Status**: Ready for planning  
**Input**: Deliver general startup/retry/readiness support upstream before Foundation.Host and Workbench adoption.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Observe readiness without starting work (Priority: P1)

A host checks whether a named shell currently has a successfully settled active generation. Checking readiness must neither start a cold shell nor wait for unfinished activation work.

**Why this priority**: Provisional publication can precede successful activation and can subsequently roll back; it is insufficient evidence for readiness.

**Independent Test**: Hold activation acceptance open, observe the named shell repeatedly, and verify it is absent from settled observations until acceptance succeeds. Observe a cold or unknown name and verify no activation or discovery work starts.

**Acceptance Scenarios**:

1. **Given** a cold or unknown shell, **When** readiness is observed, **Then** no settled generation is returned and no work is started.
2. **Given** an initially published candidate with activation acceptance still pending, **When** readiness is observed, **Then** no settled generation is returned while the observation completes independently of the pending activation.
3. **Given** successful activation including completion callbacks, **When** readiness is observed, **Then** the exact current settled generation is returned.
4. **Given** rejected initial activation, **When** rollback finishes, **Then** no settled generation is returned.

### User Story 2 - Observe the current generation across reload (Priority: P1)

A host continues checking current readiness after initial startup. Each reload must earn readiness independently of a prior startup result.

**Why this priority**: Startup success is terminal for the startup run; it cannot certify later generations.

**Independent Test**: Begin with a settled generation, pause a replacement before publication and during activation acceptance, then exercise both successful settlement and rejection with rollback.

**Acceptance Scenarios**:

1. **Given** a replacement being prepared before publication, **When** readiness is observed, **Then** the prior current settled generation remains observable.
2. **Given** a published provisional replacement, **When** readiness is observed, **Then** neither the replacement nor a historical generation is reported as current settled readiness.
3. **Given** replacement rejection, **When** rollback restores the prior active generation, **Then** that prior generation is observable again.
4. **Given** replacement success, **When** readiness is observed, **Then** the replacement is observable and the prior generation is not.
5. **Given** a current generation that has started draining or been removed, **When** readiness is observed, **Then** it is not reported as settled active.

### Edge Cases

- Invalid names follow the existing registry read validation; shell-name matching remains case insensitive.
- Completion callback failures keep their existing diagnostic-only semantics; this feature does not turn them into activation rejection.
- A generation removed during completion is not observable as settled active.
- A concurrent replacement or drain may occur after observation returns. Observation grants no use lease or future availability guarantee.
- Third-party registries may lack this observation capability. Hosts can distinguish unsupported capability from a supported observation with no settled generation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Consumers MUST be able to observe the exact current active generation only after successful activation settlement, including completion callback processing and final eligibility.
- **FR-002**: Observation MUST perform no blueprint discovery, shell composition, initialization, activation, reload, lifecycle callback, or provider resolution.
- **FR-003**: Observation MUST complete without waiting for the named shell's activation or reload transaction.
- **FR-004**: Observation MUST return no generation for unknown, inactive, provisional, draining, or removed current state.
- **FR-005**: Observation MUST recheck current generation identity and active state before returning a settled generation, without promising its future lifetime.
- **FR-006**: Existing routing visibility and startup-run terminal behavior MUST remain unchanged.
- **FR-007**: Existing third-party registries MUST remain usable without adopting the optional observation capability; host policy for unsupported capability stays with the host.
- **FR-008**: The same semantics MUST be available on all three supported runtime generations.

### Key Entities

- **Shell generation**: One composition and lifetime identified by shell name and generation number.
- **Settled active observation**: A point-in-time observation of the current active generation after successful activation settlement; no ownership or lifetime is transferred.

## Assumptions and Boundaries

This is a generic observation capability within the existing startup/readiness feature. It does not add health endpoints, decide readiness HTTP status, change application startup defaults, add retries, alter routing publication, or implement pruning/unloading. The separate package-store admission decision cannot invalidate this work and remains deferred to store operations. Existing activation settlement is the authority; no duplicate state machine is introduced.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All controlled pending and rejected initial/reload scenarios produce zero provisional settled-readiness observations.
- **SC-002**: Cold and unknown observations cause zero discovery, build, and activation operations.
- **SC-003**: Successful reload and rollback each expose exactly the correct current settled generation, with zero historical-generation substitutions.
- **SC-004**: An external consumer demonstrates the same observations on each of the three supported runtimes using publicly published packages.
