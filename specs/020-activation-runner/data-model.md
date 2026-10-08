# Data Model: Opt-in Shell Activation Runner

## Activation run

- Immutable ordered set of validated, first-occurrence, case-insensitively deduplicated target names.
- One initial-pass task, one linked run lifetime, and zero or one retry loop per target.
- A public snapshot of immutable per-target states.
- An explicitly stopped state that prevents further retries and external reconciliation for this run. A policy-stopped target may still be externally reconciled until the run itself is stopped.

## Activation attempt

- Target name and one-based attempt number.
- Outcome: succeeded, activation failure, or not-current.
- Started and completed timestamps from the configured `TimeProvider`.
- Optional safe error code/type for state projection.
- Optional raw exception is transient callback input only; it is never copied into a public snapshot.

## Target state

- Name and attempt count.
- Status: pending, attempting, retry scheduled, succeeded, satisfied externally, failed, stopped, or policy error.
- Last attempt outcome/timestamps and optional next-attempt deadline.
- Safe error code/type only; no exception, message, stack, or host-specific payload.
- Succeeded and externally satisfied are terminal for the run.

## Retry decision

- Stop, or RetryAfter with a strictly positive delay.
- Invalid action/delay, deadline overflow, or policy exception fails closed for this target and leaves other targets independently schedulable.
