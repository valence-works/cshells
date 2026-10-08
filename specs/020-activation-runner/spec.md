# Feature Specification: Opt-in Shell Activation Runner

**Feature Branch**: `020-activation-runner`
**Created**: 2026-10-08
**Status**: Draft
**Input**: User description: "Add opt-in generic shell activation runner with serial initial pass, retries, and run-state ownership"

## User Scenarios & Testing

### User Story 1 - Host explicitly starts and observes shell activation (Priority: P1)

A host starts one run for an ordered set of shell names. The runner attempts each name once in order, exposes a safe snapshot, and optionally retries unsuccessful targets according to a host-supplied policy. The host can stop the run with a bounded wait. Registering CShells alone does not start or register a runner.

**Why this priority**: Hosts need a reusable unit for initial activation and retries without changing CShells' default lazy activation behavior or coupling the runtime to a host framework.

**Independent Test**: Register the runner explicitly, start it with multiple names and a controlled registry, then verify ordered initial attempts, observable state, retry timing, and bounded stop behavior.

**Acceptance Scenarios**:

1. **Given** an explicit runner registration, **When** a host starts a run, **Then** it receives a handle immediately and the runner attempts targets serially in caller order.
2. **Given** one target's initial attempt fails and a retry policy is supplied, **When** the complete initial pass finishes, **Then** retries begin only after that boundary and follow the policy's delay.
3. **Given** no retry policy or a policy stop decision, **When** an attempt ends unsuccessfully, **Then** that target receives no further attempt in the run.
4. **Given** a settled current shell already exists or becomes active externally, **When** the runner reconciles a target, **Then** the target becomes satisfied without an owned activation attempt.
5. **Given** a caller cancels startup or stops a run, **When** owned work observes cancellation, **Then** no later retry is scheduled; a stubborn activation remains tracked after a bounded stop wait.
6. **Given** target names contain duplicates with different casing, **When** a run starts, **Then** it keeps the first occurrence and caller order; an empty list completes without attempts.
7. **Given** only `AddCShells` is used, **When** services are inspected, **Then** no runner is registered or started and child shell providers do not receive the root runner.

## Edge Cases

- A candidate shell is visible but not committed, or is replaced/removed before the runner verifies it; this is not success.
- A not-current result has no fabricated exception but remains eligible for policy-directed retry.
- A policy throws, returns an invalid delay/action, or produces a deadline overflow; only that target fails closed and retains its last activation error.
- An observer or logger throws; state remains updated and retry scheduling continues.
- Stop is called repeatedly, during a delay, during a cancellation-aware activation, or while activation ignores cancellation.
- Snapshot is read from inside the observer and must not deadlock.
- Run shutdown does not drain shells and a satisfied target is not reactivated after its generation later drains.

## Requirements

### Functional Requirements

- **FR-001**: Hosts MUST opt into the runner explicitly; ordinary CShells registration MUST remain lazy.
- **FR-002**: A run MUST validate and copy names synchronously, reject null/blank names, deduplicate case-insensitively while preserving first-occurrence order, and accept an empty list.
- **FR-003**: A run MUST return its handle without invoking a blocking activation on the caller's stack and MUST attempt initial targets serially.
- **FR-004**: Retry scheduling MUST begin only after the initial pass completes normally; actual retry deadlines MUST be armed from scheduler start.
- **FR-005**: Retry policy and observer input MUST include structured attempt outcome and timestamps; public snapshots MUST contain only safe error fields, never exception objects, messages, or stacks.
- **FR-006**: An attempt MUST count as successful only when the returned shell remains the current instance and, for a concrete CShells shell, has a committed activation marker.
- **FR-007**: The runner MUST reconcile externally satisfied targets only from a known concrete settled CShells shell; unknown custom shell implementations are not externally reconciled.
- **FR-008**: Each target MUST have one run-owned attempt schedule, cumulative failure count and first/last failure times, retry-delay/deadline state, and nullable verified generation; a satisfied target MUST remain terminal with its first satisfied generation for that run.
- **FR-009**: Observer and retry-policy callbacks MUST execute outside internal synchronization; observer failures MUST NOT change the attempt result or retry schedule.
- **FR-010**: Stop MUST request cancellation, observe all owned work, honor its shutdown wait token, and defer linked cancellation-source disposal until operations using it finish.
- **FR-011**: A bounded Stop MUST NOT abandon or lose a still-running activation; its eventual result/fault MUST be observed and the run's lifetime resources cleaned up.
- **FR-012**: Stop MUST be idempotent and MUST NOT drain registry shells.
- **FR-013**: A host MAY supply its own runner, and the registered runner MUST remain root-only rather than being copied into shell providers.

## Key Entities

- **Activation run**: One host-owned operation over an immutable, ordered set of shell names with a lifetime, initial-pass boundary, and target snapshots.
- **Activation attempt**: One owned registry call and its verified outcome, timestamps, safe error fields, and transient exception supplied to policy/observer callbacks only.
- **Target state**: Immutable public view of one target's attempt count, last outcome, scheduling status, next retry deadline, and safe error fields.
- **Retry decision**: Stop scheduling or request a positive delay for a target's next attempt.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Every accepted target receives its initial attempt in supplied first-occurrence order, and no initial target is skipped because another target failed.
- **SC-002**: Zero retries begin before the initial pass completes; all retry deadlines are derived from the scheduler's actual start time.
- **SC-003**: No provisional or replaced candidate is reported as a successful target; only a settled current shell satisfies the target.
- **SC-004**: Public snapshots contain no raw exception object, exception message, or stack trace.
- **SC-005**: Stop cancellation and bounded waits leave no unobserved operation faults or premature linked-token-source disposal.

Target-state history is cumulative within one run: failure count and first/last failure timestamps survive later success or external satisfaction. The latest outcome, attempt timestamps, and attempt error fields describe the latest owned attempt. Retry delay describes a currently scheduled retry and clears when scheduling ends or that attempt begins. A policy-stopped or policy-error target can still be recognized as externally satisfied until explicit run shutdown; shutdown freezes reconciliation.
