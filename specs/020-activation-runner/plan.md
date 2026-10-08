# Implementation Plan: Opt-in Shell Activation Runner

**Branch**: `020-activation-runner` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/020-activation-runner/spec.md`

## Summary

Add a generic, opt-in activation run API in CShells abstractions and runtime. Each run copies/deduplicates its target names, performs one serial initial pass, exposes immutable safe state, and optionally schedules one retry loop per unsuccessful target after the initial pass completes. The run owns cancellation, retry decisions, delays, operation observation, and linked-token-source cleanup. Default-registry reconciliation uses the concrete `Shell.IsActivationCommitted` marker from #148; custom registries rely on their settled `GetOrActivateAsync` return contract and current identity. No host integration or default startup behavior is added.

## Technical Context

**Language/Version**: C# 14; runtime packages multi-target net8.0, net9.0, net10.0
**Primary Dependencies**: Existing Microsoft.Extensions DI/logging/time abstractions; test-only `Microsoft.Extensions.TimeProvider.Testing` 10.8.0
**Storage**: None
**Testing**: xUnit 2.x; test project targets net10.0
**Target Platform**: .NET 8, .NET 9, and .NET 10
**Project Type**: .NET abstractions and runtime library
**Performance Goals**: No startup wait on the Start caller stack; serial initial attempts; independent per-target retry workers; no global lock across callbacks or registry calls
**Constraints**: Explicit registration only; no IHostedService or host adapters; immutable public snapshots; no exception message/stack in snapshots; bounded Stop waits only when caller supplies a shutdown token
**Scale/Scope**: Public contracts, runtime run/runner, DI extension and root exclusion, one focused integration suite, central test-only package pin

## Constitution Check

- Principle I: Put public consumer contracts in `CShells.Abstractions`; runtime implementation stays in `CShells`.
- Principle III: Use existing C# 14 / .NET 8-10 targets; testing helper package is test-only and is pinned centrally at the root-approved version 10.8.0.
- Principle IV: Invalid input and retry decisions fail clearly; per-target policy errors are represented safely in snapshots.
- Principle V: Add deterministic xUnit coverage for ordering, retries, cancellation, state privacy, DI behavior, default-registry settlement, and cleanup.
- Principle VI: Use one coordinator task per run and one retry worker per target; add no host coordination, public stable-state capability, or lifecycle subscription.
- Principle VII: Keep registry/policy/observer/time callbacks outside state gates; use cancellation-aware delays and preserve a tracked task until all work finishes.

No constitution exception is required.

## Project Structure

```text
src/CShells.Abstractions/Hosting/
├── IShellActivationRunner.cs
├── IShellActivationRun.cs
├── IShellActivationAttemptObserver.cs
├── ShellActivationAttempt.cs
├── ShellActivationAttemptState.cs
└── ShellActivationRetryDecision.cs

src/CShells/Hosting/
├── ShellActivationRunner.cs
├── ShellActivationRun.cs
└── DefaultShellServiceExclusionProvider.cs

src/CShells/DependencyInjection/
└── ShellActivationRunnerServiceCollectionExtensions.cs

tests/CShells.Tests/Integration/Hosting/
└── ShellActivationRunnerTests.cs
```

**Structure Decision**: Public contracts live in the abstraction assembly; the runner and run handle live in the implementation assembly; one extension registers the root singleton only when requested. Add `IShellActivationRunner` to child-service exclusions. Keep the runner out of `AddCShells` and do not add an `IHostedService`.

## Run Ownership and State

- `Start` validates and copies the ordered target list, then schedules a tracked coordinator with `Task.Run`; it never executes a blocking registry activation on the caller's stack.
- The coordinator attempts initial targets serially and continues after ordinary per-target activation failures. The `InitialPass` task marks the explicit boundary. Retry timers and `NextAttemptAt` values are created only after this task completes normally.
- The run owns a linked lifetime token source, one coordinator task, and one retry loop for each target with a retry decision. The coordinator awaits all retry loops; the task remains tracked when cancellation is ignored. CTS disposal occurs only when the coordinator and cancellation callbacks have completed.
- Public snapshots are immutable safe records. They contain target, count, last outcome/status, timestamps, retry deadline, and safe error code/type, never an exception object, message, or stack. Raw exceptions are supplied only to policy/observer callback input and are not retained as public state.
- State replacement uses a short private gate. Registry calls, retry policy, observer, logging, and delay occur outside it. Observer faults are isolated; safe logging is also guarded.
- Successful returned shells must still be current by reference. A concrete CShells `Shell` additionally must have `IsActivationCommitted`. External reconciliation recognizes only a current, committed concrete `Shell`; unknown custom shell implementations are not reconciled.
- Satisfaction is terminal for one run. Policy-stopped or policy-error targets may still become externally satisfied before explicit run shutdown. Stop freezes further reconciliation/scheduling and cancels owned work; it does not drain shells. Stop joins the coordinator only through its caller-provided shutdown token and remains idempotent.

## Retry Scheduling

- No policy means one attempt per target.
- Policies receive a structured failed or not-current attempt. A not-current attempt has no synthetic exception.
- A retry decision must be exactly Stop or RetryAfter with a positive delay. An unknown action, non-positive delay, deadline overflow, or policy exception fails only that target, records a safe policy-error state, and preserves the last activation error.
- Delays use an optional root `TimeProvider`, falling back to `TimeProvider.System`; retry deadline is published when the scheduler begins waiting, not during the initial pass.

## Package and Lockfile

Pin `Microsoft.Extensions.TimeProvider.Testing` 10.8.0 in the central test dependency group and reference it only from `tests/CShells.Tests`. Validate with a real NuGet restore/build. This package has no production dependency and no net10 transitive package dependencies. Update a test lock file only if the repository actually has one for this project.

## Verification

Use `FakeTimeProvider` for retry timers and `TaskCompletionSource` gates for activation, commit, replacement, and cancellation races. Verify retry start after the serial initial pass, policy input and sanitization, stop during retry/activation, delayed CTS disposal for stubborn activation, callback reentry into Snapshot, DI opt-in/root-only behavior, and default-registry committed-marker integration. Mutate the runner to accept a provisional active shell or schedule retries before the initial-pass boundary; a causal regression must fail and pass after restoring the fix. Run focused runner and existing registry lifecycle tests plus a net8/net9/net10 CShells build. Root owns the combined suite.
