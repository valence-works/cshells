# Feature Specification: Returned Generation Diagnostics

**Feature Branch**: `025-returned-generation`  
**Created**: 2026-10-09  
**Status**: In progress — local implementation qualified; review, hosted gates and public-package acceptance pending  
**Input**: GitHub issue #167, “Expose the returned generation in activation-run diagnostics”

## User Scenarios & Testing

### User Story 1 - Identify the generation returned by an activation attempt (Priority: P1)

As a host or diagnostic consumer, I need an activation-run result to identify the generation returned by that run's successful activation call, including when a custom shell registry is used, so I can associate the completed attempt with the shell generation it actually obtained.

**Why this priority**: Custom registry successes currently have no generation association in the run result, while the existing verified-generation field intentionally remains specific to the built-in shell implementation. Consumers need both meanings without weakening or changing the existing one.

**Independent Test**: Run an activation against built-in and custom registries and inspect the attempt and terminal run snapshot. A successful current return reports its descriptor generation; non-success and externally satisfied results do not invent a returned generation.

**Acceptance Scenarios**:

1. **Given** a built-in registry returns the current shell successfully, **When** the attempt completes, **Then** attempt and terminal snapshot report that returned shell's descriptor generation, and the existing verified-generation value retains its current meaning.
2. **Given** a custom registry returns the current shell successfully, **When** the attempt completes, **Then** attempt and terminal snapshot report that returned shell's descriptor generation while verified-generation remains null.
3. **Given** an activation is not current, fails, or is canceled before returning successfully, **When** its result is observed, **Then** no returned generation is reported.
4. **Given** a target is satisfied externally and no in-flight call later returns successfully, **When** its terminal snapshot is observed, **Then** returned-generation remains absent and the existing verified-generation behavior is unchanged.
5. **Given** external satisfaction occurs while this run already has an activation call in flight, **When** that call later returns the current shell successfully, **Then** the attempt and snapshot report that call's returned generation while external terminal status and verified-generation retain their existing behavior.
6. **Given** the run has recorded its successful returned generation, **When** a later reload or external generation change occurs, **Then** the terminal snapshot continues to report the generation associated with that successful return.
7. **Given** a caller compiled against an existing public activation-result constructor, **When** it continues to construct the result, **Then** that constructor signature remains available and the additive returned-generation metadata defaults to absent.

## Edge Cases

- A registry may return a shell whose identity is no longer current; this is `NotCurrent`, not a successful returned-generation result.
- A custom registry may successfully return a current shell without providing the built-in registry's concrete settlement signal. Returned-generation reports the successful return's descriptor generation; verified-generation remains null.
- A run may satisfy a target externally while an activation call is still in flight. External settlement does not itself populate returned-generation; if the in-flight call later returns the current shell successfully, that call's own returned generation is reported without changing the existing external terminal status or verified-generation.
- Cancellation after an activation call has already returned successfully does not rewrite that successful attempt as a failed return; cancellation of the call itself produces no returned generation.
- A later registry change must not rewrite an already terminal attempt or its historical generation association.
- The metadata must not keep a shell, provider, or other generation-owned object alive.

## Requirements

### Functional Requirements

- **FR-001**: The activation attempt and its corresponding run-state snapshot MUST expose nullable scalar metadata identifying the descriptor generation returned by that attempt's own successful activation call.
- **FR-002**: The returned-generation value MUST be set only when the call returns successfully and the runner accepts that returned shell as the current result for the target.
- **FR-003**: The returned-generation value MUST remain absent for `NotCurrent`, failed, and call-canceled outcomes, and for targets satisfied externally without a successful return from that run.
- **FR-004**: The existing verified-generation field MUST retain its current built-in verification and external-settlement semantics, including remaining null for a successful custom registry result where it is null today.
- **FR-005**: A terminal attempt's returned-generation value MUST remain stable when the registry later publishes or settles another generation.
- **FR-006**: Adding returned-generation metadata MUST NOT change existing public constructor signatures or require consumers to retain shell or provider instances.
- **FR-007**: Public API documentation and tests MUST distinguish returned-generation from verified-generation.

### Key Entities

- **Activation attempt**: The outcome of one target activation call, including its status and any scalar generation associated with a successful current return.
- **Target run snapshot**: The immutable observable state for a named activation target, including terminal outcome and the historical generation associated with its successful return, if any.
- **Shell generation**: The generation identifier exposed by the descriptor of a shell returned by an activation call.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A successful current return through either built-in or custom registry paths reports the exact returned descriptor generation in both the attempt and terminal snapshot.
- **SC-002**: All existing verified-generation assertions pass unchanged, including custom-success null behavior and external-settlement behavior.
- **SC-003**: Tests demonstrate that `NotCurrent`, failure, call cancellation, and external satisfaction without a successful own return do not fabricate returned-generation values.
- **SC-004**: A deterministic later-generation change leaves the original terminal returned-generation association unchanged.
- **SC-005**: Existing public activation-result constructor signatures remain callable without supplying the new metadata.

## Assumptions

- A successful call's returned shell descriptor is the authoritative source for the generation associated with that call.
- “Canceled” means the activation call did not successfully return; cancellation requested after a successful return does not erase that call's result.
- This feature adds diagnostics only. It does not change activation success, registry settlement, external reconciliation, routing, retries, or readiness policy.
