using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Lifecycle;

/// <summary>
/// The default <see cref="IShell"/> implementation. Monotonic lifecycle state is maintained
/// via <see cref="Interlocked.CompareExchange(ref int, int, int)"/> on an integer backing
/// field; state reads go through <see cref="Volatile"/><c>.Read</c>.
/// </summary>
/// <remarks>
/// Shell disposal is registry-owned. <see cref="DisposeAsync"/> is <c>internal</c> — hosts
/// observe disposal via the <see cref="ShellLifecycleState.Drained"/> →
/// <see cref="ShellLifecycleState.Disposed"/> transition event raised through the registered
/// <c>onStateChanged</c> callback.
/// </remarks>
internal sealed class Shell(
    ShellDescriptor descriptor,
    IServiceProvider serviceProvider,
    Func<IShell, ShellLifecycleState, ShellLifecycleState, Task> onStateChanged) : IShell
{
    private readonly Func<IShell, ShellLifecycleState, ShellLifecycleState, Task> _onStateChanged = Guard.Against.Null(onStateChanged);
    private int _state = (int)ShellLifecycleState.Initializing;
    private int _activeScopes;

    // Signals waiters (drain phase 1) whenever the scope counter drops. Created lazily by
    // the drain path; written to atomically.
    private TaskCompletionSource<bool>? _scopesChangedSignal;

    // Single-shot guard over the ServiceProvider teardown. Published by the first caller into
    // DisposeAsync; concurrent callers observe the same completion task instead of racing into
    // a second ServiceProvider.DisposeAsync() (drain + emergency-dispose can overlap at
    // shutdown, see CShellsStartupHostedService).
    private Task? _disposeTask;

    // CAS-published in-flight drain. Non-null while State is Deactivating/Draining/Drained;
    // cleared back to null in DisposeCoreAsync to break the reference cycle for GC. The
    // registry calls PublishDrain(...) once per generation; subsequent concurrent callers
    // observe the published instance and return early.
    private DrainOperation? _drain;

    /// <inheritdoc />
    public ShellDescriptor Descriptor { get; } = Guard.Against.Null(descriptor);

    /// <inheritdoc />
    public IServiceProvider ServiceProvider { get; } = Guard.Against.Null(serviceProvider);

    /// <inheritdoc />
    public ShellLifecycleState State => (ShellLifecycleState)Volatile.Read(ref _state);

    /// <inheritdoc />
    public IDrainOperation? Drain => Volatile.Read(ref _drain);

    /// <summary>
    /// CAS-publishes <paramref name="candidate"/> as the in-flight drain operation for this shell.
    /// Returns the winner — equal to <paramref name="candidate"/> on the first call, or the
    /// previously-published instance for any subsequent concurrent caller. Idempotent.
    /// </summary>
    internal DrainOperation PublishDrain(DrainOperation candidate)
    {
        Guard.Against.Null(candidate);
        var winner = Interlocked.CompareExchange(ref _drain, candidate, null);
        return winner ?? candidate;
    }

    /// <summary>Current active-scope count. Exposed for diagnostics.</summary>
    internal int ActiveScopeCount => Volatile.Read(ref _activeScopes);

    /// <inheritdoc />
    public IShellScope BeginScope()
    {
        // BeginScope during Initializing is permitted (initializers may open scopes).
        // BeginScope during Draining is permitted per FR-022 (the new scope joins the counter
        // and delays phase-1 completion until released). BeginScope after Disposed throws.
        //
        // Close the check-then-increment race with a post-check: increment first, then verify
        // the shell didn't transition to Disposed in between. If it did, roll back the counter
        // so drain's scope-wait isn't misled by a phantom scope. (The final race — disposal
        // landing between the post-check and CreateAsyncScope — still raises
        // ObjectDisposedException from the provider, which is the correct observable error.)
        Interlocked.Increment(ref _activeScopes);
        if (State == ShellLifecycleState.Disposed)
        {
            DecrementScopeCounter();
            throw new InvalidOperationException($"Shell {Descriptor} is Disposed; cannot open a new scope.");
        }

        try
        {
            var inner = ServiceProvider.CreateAsyncScope();
            return new ShellScope(this, inner);
        }
        catch
        {
            // If scope construction throws (e.g., post-check race with disposal), roll back
            // the counter so drain isn't deadlocked.
            DecrementScopeCounter();
            throw;
        }
    }

    /// <summary>
    /// Decrements the active-scope counter and signals any pending drain-phase-1 waiter.
    /// Invoked by <see cref="ShellScope.DisposeAsync"/>.
    /// </summary>
    internal void DecrementScopeCounter()
    {
        var remaining = Interlocked.Decrement(ref _activeScopes);
        if (remaining == 0)
            Volatile.Read(ref _scopesChangedSignal)?.TrySetResult(true);
    }

    /// <summary>
    /// Returns a task that completes when the active-scope counter reaches zero. Used by
    /// the drain operation's phase-1 waiter; the caller is responsible for bounding it with
    /// a deadline.
    /// </summary>
    internal Task WaitForScopesReleasedAsync()
    {
        if (ActiveScopeCount == 0)
            return Task.CompletedTask;

        // Lazily create the signal; idempotent via CAS.
        var signal = Volatile.Read(ref _scopesChangedSignal);
        if (signal is null)
        {
            var candidate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            signal = Interlocked.CompareExchange(ref _scopesChangedSignal, candidate, null) ?? candidate;
        }

        // Re-check after publishing the signal: a concurrent DecrementScopeCounter may have
        // brought the counter to zero in between our first read and the signal creation.
        if (ActiveScopeCount == 0)
            signal.TrySetResult(true);

        return signal.Task;
    }

    /// <summary>
    /// Attempts to transition from <paramref name="expected"/> to <paramref name="next"/>.
    /// Returns <c>true</c> when the CAS succeeds.
    /// </summary>
    internal async Task<bool> TryTransitionAsync(ShellLifecycleState expected, ShellLifecycleState next)
    {
        if (next <= expected)
            throw new ArgumentOutOfRangeException(nameof(next), "State transitions must advance forward.");

        var expectedInt = (int)expected;
        if (Interlocked.CompareExchange(ref _state, (int)next, expectedInt) != expectedInt)
            return false;

        await _onStateChanged(this, expected, next).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Force-advances this shell to <paramref name="target"/>, but only if the current state
    /// is strictly less than <paramref name="target"/>. Used by the drain path to move
    /// <c>Draining → Drained → Disposed</c>, and by the registry's emergency-dispose path
    /// on host shutdown-timeout breach.
    /// </summary>
    internal async Task ForceAdvanceAsync(ShellLifecycleState target)
    {
        while (true)
        {
            var prevInt = Volatile.Read(ref _state);
            if (prevInt >= (int)target)
                return;

            if (Interlocked.CompareExchange(ref _state, (int)target, prevInt) == prevInt)
            {
                await _onStateChanged(this, (ShellLifecycleState)prevInt, target).ConfigureAwait(false);
                return;
            }
        }
    }

    /// <summary>
    /// Disposes the shell's service provider and transitions the shell to
    /// <see cref="ShellLifecycleState.Disposed"/>. Registry-only entry point. Concurrent
    /// callers observe the same disposal task — the underlying provider teardown runs
    /// exactly once.
    /// </summary>
    internal ValueTask DisposeAsync()
    {
        var existing = Volatile.Read(ref _disposeTask);
        if (existing is not null)
            return new ValueTask(existing);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var winner = Interlocked.CompareExchange(ref _disposeTask, tcs.Task, null);
        if (winner is not null)
            return new ValueTask(winner);

        return new ValueTask(DisposeCoreAsync(tcs));
    }

    private async Task DisposeCoreAsync(TaskCompletionSource tcs)
    {
        try
        {
            // Clear the drain reference BEFORE advancing to Disposed so subscribers notified of
            // the Drained → Disposed transition observe the documented IShell.Drain invariant
            // ("null when the state is Disposed") at the moment of the transition. Also breaks
            // the Shell ↔ DrainOperation reference cycle so both become GC-eligible together
            // once the registry releases its slot reference.
            Volatile.Write(ref _drain, null);

            await ForceAdvanceAsync(ShellLifecycleState.Disposed).ConfigureAwait(false);

            switch (ServiceProvider)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
            tcs.TrySetResult();
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
            throw;
        }
    }
}
