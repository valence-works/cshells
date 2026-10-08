# Contract: Shell Activation Runner

The public surface lives in `CShells.Abstractions` under `CShells.Hosting`. Names below are the approved shape; implementation may adjust names while preserving behavior.

```csharp
public interface IShellActivationRunner
{
    IShellActivationRun Start(
        IReadOnlyList<string> shellNames,
        ShellActivationRetryPolicy? retryPolicy = null,
        IShellActivationAttemptObserver? observer = null,
        CancellationToken startupCancellationToken = default);
}

public interface IShellActivationRun
{
    Task InitialPass { get; }
    IReadOnlyList<ShellActivationAttemptState> Snapshot { get; }
    Task StopAsync(CancellationToken shutdownToken = default);
}

public delegate ShellActivationRetryDecision ShellActivationRetryPolicy(ShellActivationAttempt attempt);

public interface IShellActivationAttemptObserver
{
    ValueTask OnAttemptCompletedAsync(ShellActivationAttempt attempt, CancellationToken cancellationToken);
}
```

`ShellActivationAttempt` is the transient callback input and may include the raw exception for host policy/classification and the verified generation on concrete-shell success. `ShellActivationAttemptState` is the immutable public snapshot projection and must contain only safe error code/type, never an exception object, message, stack, connection string, or host command. A not-current attempt has outcome `NotCurrent` and a null exception.

Target snapshots expose cumulative `FailedAttemptCount`, `FirstFailedAt`, and `LastFailedAt`; these survive later success or external satisfaction. `LastOutcome`, attempt timestamps, and error fields describe the latest owned attempt. `RetryDelay` is scheduling state and clears when a retry begins or scheduling ends. `VerifiedGeneration` is the generation first verified as successful or externally satisfied, and remains stable once satisfied. Failed, policy-stopped, and policy-error targets may still become externally satisfied before explicit run shutdown.

Retry decisions distinguish Stop and RetryAfter. RetryAfter must carry a positive delay whose deadline can be represented by the configured clock. Invalid decision, overflow, or policy exception ends scheduling only for that target, preserves the last attempt's safe error data, and records a safe policy-error state.

The runner is registered only by `IServiceCollection.AddShellActivationRunner()`. Registration is TryAdd-style and may precede `AddCShells`; it does not add a hosted service. The runner is excluded from child shell providers.

`Start` validates/copies names synchronously and returns a run handle without invoking registry activation on the caller's stack. Initial attempts are serial. Retry timers are not created until `InitialPass` completes normally. Stop requests cancellation and joins run-owned work only through the supplied shutdown token; it does not drain shells. A stopped run no longer reconciles external state. Same-run Stop from within its own observer/policy callback is unsupported because it joins that callback's work; Snapshot reentry is supported.

For a successful registry return, current identity must match by reference. A concrete CShells `Shell` additionally requires `IsActivationCommitted`. External reconciliation is supported only for a current committed concrete `Shell`, not an unknown custom shell implementation, even when the registry itself is custom or wrapped.
