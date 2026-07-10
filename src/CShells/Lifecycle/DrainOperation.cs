using System.Diagnostics;
using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Lifecycle;

/// <summary>
/// Default <see cref="IDrainOperation"/>. Coordinates four drain phases:
/// (1) scope wait bounded by the deadline; (2) parallel handler invocation; (3) grace after
/// deadline or force; (4) sequential <see cref="IShellTerminator"/> invocation while the shell
/// is <see cref="ShellLifecycleState.Drained"/> and its provider is still alive. Exposes itself
/// as <see cref="IDrainExtensionHandle"/> to handlers, delegating extension requests to the
/// configured <see cref="IDrainPolicy"/>.
/// </summary>
internal sealed class DrainOperation : IDrainOperation, IDrainExtensionHandle
{
    private readonly Shell _shell;
    private readonly IDrainPolicy _policy;
    private readonly TimeSpan _gracePeriod;
    private readonly ILogger<DrainOperation> _logger;
    private readonly TaskCompletionSource<DrainResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _cancelSource = new();
    private DateTimeOffset? _deadline;
    private int _status = (int)DrainStatus.Pending;
    private int _force;

    public DrainOperation(Shell shell, IDrainPolicy policy, TimeSpan gracePeriod, ILogger<DrainOperation>? logger = null)
    {
        _shell = Guard.Against.Null(shell);
        _policy = Guard.Against.Null(policy);
        _gracePeriod = gracePeriod > TimeSpan.Zero ? gracePeriod : TimeSpan.FromSeconds(3);
        _logger = logger ?? NullLogger<DrainOperation>.Instance;

        if (!_policy.IsUnbounded && _policy.InitialTimeout is { } t)
            _deadline = DateTimeOffset.UtcNow.Add(t);
    }

    /// <inheritdoc />
    public DrainStatus Status => (DrainStatus)Volatile.Read(ref _status);

    /// <inheritdoc />
    public DateTimeOffset? Deadline => _deadline;

    /// <inheritdoc />
    public Task<DrainResult> WaitAsync(CancellationToken cancellationToken = default) =>
        _completion.Task.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public Task ForceAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _force, 1) != 0)
            return Task.CompletedTask;

        // Drain may have already completed — the CTS is disposed in that case and calling
        // Cancel() would throw ObjectDisposedException. Checking the completion task (rather
        // than status, which flips before terminators run) makes ForceAsync a clean no-op
        // after completion while still able to interrupt a hung terminator.
        if (_completion.Task.IsCompleted)
            return Task.CompletedTask;

        try
        {
            _cancelSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Raced with drain completion between the status check and Cancel(); safe to ignore.
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public bool TryExtend(TimeSpan requested, out TimeSpan granted)
    {
        if (!_policy.TryExtend(requested, out granted))
            return false;

        // Extend the deadline the phase-2 cancel timer observes. For simplicity we only extend
        // forward from "now" rather than from the original deadline — matches the typical use
        // case where a handler asks for more time because work is still outstanding.
        _deadline = DateTimeOffset.UtcNow.Add(granted);
        return true;
    }

    /// <summary>
    /// Runs drain phases 1–3 in the background. Safe to call once. Any exception is captured
    /// into the completion task; callers surface it via <see cref="WaitAsync"/>.
    /// </summary>
    internal Task RunAsync()
    {
        return Task.Run(async () =>
        {
            try
            {
                var result = await ExecuteAsync().ConfigureAwait(false);
                _completion.TrySetResult(result);
            }
            catch (Exception ex)
            {
                _completion.TrySetException(ex);
            }
            finally
            {
                _cancelSource.Dispose();
            }
        });
    }

    private async Task<DrainResult> ExecuteAsync()
    {
        // Phase 1: scope wait.
        var scopeWaitStart = Stopwatch.GetTimestamp();
        var abandonedScopes = await AwaitScopeReleaseAsync().ConfigureAwait(false);
        var scopeWaitElapsed = Stopwatch.GetElapsedTime(scopeWaitStart);

        // Phase 2: handler invocation. Linked CTS: deadline or force.
        var handlerResults = await InvokeHandlersAsync().ConfigureAwait(false);

        // Determine overall status. Resolved before terminators run, so terminator outcomes
        // structurally cannot affect the drain status.
        var status = ResolveStatus(handlerResults);
        Volatile.Write(ref _status, (int)status);

        await _shell.ForceAdvanceAsync(ShellLifecycleState.Drained).ConfigureAwait(false);

        // Phase 4: terminator invocation. The shell is Drained; its provider is still alive.
        var terminatorResults = await InvokeTerminatorsAsync().ConfigureAwait(false);

        // Transition to Disposed (disposes provider).
        await _shell.DisposeAsync().ConfigureAwait(false);

        return new DrainResult(_shell.Descriptor, status, scopeWaitElapsed, abandonedScopes, handlerResults, terminatorResults);
    }

    private async Task<int> AwaitScopeReleaseAsync()
    {
        var scopeWaitTask = _shell.WaitForScopesReleasedAsync();
        if (scopeWaitTask.IsCompletedSuccessfully)
            return _shell.ActiveScopeCount;

        var remaining = _deadline is null
            ? Timeout.InfiniteTimeSpan
            : _deadline.Value - DateTimeOffset.UtcNow;

        if (remaining <= TimeSpan.Zero && _deadline is not null)
            return _shell.ActiveScopeCount;

        // Always wire the cancellation token into the timeout task — even for the unbounded
        // path (remaining == InfiniteTimeSpan). A ForceAsync call must be able to interrupt
        // the scope-wait and skip straight to handler invocation regardless of policy.
        // Task.Delay(Timeout.InfiniteTimeSpan, token) means "wait forever unless token fires."
        var timeoutTask = Task.Delay(remaining, _cancelSource.Token);

        await Task.WhenAny(scopeWaitTask, timeoutTask).ConfigureAwait(false);

        // Whether the scope-wait completed normally or timed out / was forced, capture whatever
        // count is outstanding at this moment. Zero for the normal (clean-release) path.
        return _shell.ActiveScopeCount;
    }

    private async Task<IReadOnlyList<DrainHandlerResult>> InvokeHandlersAsync()
    {
        // Resolve handlers inside a scope so transient registrations get a fresh instance.
        // Lifetime is deferred (see continuation below) so the scope and the cancellation
        // sources outlive any abandoned handler still running after the grace period —
        // disposing them while a handler is mid-flight would yield use-after-dispose for
        // services resolved into the scope.
        var scope = _shell.ServiceProvider.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<IDrainHandler>().ToList();

        if (handlers.Count == 0)
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            return [];
        }

        var results = new DrainHandlerResult[handlers.Count];
        // Pre-seed with "not completed" defaults so abandoned handlers (those still running after
        // the grace period) leave a valid entry rather than a null slot that crashes ResolveStatus.
        for (var i = 0; i < handlers.Count; i++)
            results[i] = new DrainHandlerResult(handlers[i].GetType().Name, Completed: false, Elapsed: TimeSpan.Zero, Error: null);

        // Per-handler token: cancelled when the deadline elapses, or immediately on Force.
        var deadlineCts = new CancellationTokenSource();
        if (_deadline is { } deadline)
        {
            var until = deadline - DateTimeOffset.UtcNow;
            if (until <= TimeSpan.Zero)
                deadlineCts.Cancel();
            else
                deadlineCts.CancelAfter(until);
        }

        var combined = CancellationTokenSource.CreateLinkedTokenSource(deadlineCts.Token, _cancelSource.Token);
        var token = combined.Token;

        var tasks = new Task[handlers.Count];
        for (var i = 0; i < handlers.Count; i++)
        {
            var index = i;
            var handler = handlers[index];
            tasks[index] = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    await handler.DrainAsync(this, token).ConfigureAwait(false);
                    sw.Stop();
                    results[index] = new DrainHandlerResult(handler.GetType().Name, Completed: true, sw.Elapsed, Error: null);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    sw.Stop();
                    results[index] = new DrainHandlerResult(handler.GetType().Name, Completed: false, sw.Elapsed, Error: null);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _logger.LogWarning(ex, "Drain handler {Handler} threw for shell {Shell}", handler.GetType().FullName, _shell.Descriptor);
                    results[index] = new DrainHandlerResult(handler.GetType().Name, Completed: false, sw.Elapsed, Error: ex);
                }
            });
        }

        // Phase 3: grace wait. After the deadline/force elapses, wait up to `_gracePeriod` for
        // handlers to observe cancellation. Handlers still running after grace are abandoned.
        var allHandlers = Task.WhenAll(tasks);

        // Defer disposal of the scope and cancellation sources until every handler task —
        // including abandoned ones that outlive the grace period — has actually finished.
        // Otherwise the scope (and any services resolved into it) would be torn down while a
        // still-running handler is using them.
        _ = allHandlers.ContinueWith(
            async _ =>
            {
                try
                {
                    await scope.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Drain handler scope disposal failed for shell {Shell}", _shell.Descriptor);
                }
                combined.Dispose();
                deadlineCts.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        // Wait for either all handlers to complete, or cancellation + grace to elapse.
        if (!allHandlers.IsCompleted)
        {
            await Task.WhenAny(allHandlers, WaitForCancellationThenGrace(token)).ConfigureAwait(false);
        }

        return results.ToList();
    }

    private async Task<IReadOnlyList<ShellTerminatorResult>> InvokeTerminatorsAsync()
    {
        // Resolve terminators inside a scope so transient registrations get a fresh instance.
        // Lifetime is deferred (see continuation below) for the same reason as handler scopes:
        // an abandoned terminator may still be running after the grace period.
        AsyncServiceScope scope;
        try
        {
            scope = _shell.ServiceProvider.CreateAsyncScope();
        }
        catch (ObjectDisposedException)
        {
            // Emergency dispose (host shutdown-timeout breach) raced ahead of this drain;
            // terminators are deliberately skipped on that path.
            _logger.LogWarning("Shell {Shell} was disposed before termination; skipping terminators.", _shell.Descriptor);
            return [];
        }

        ShellTerminatorOrderingPlanner.TerminatorOrderingPlan plan;
        try
        {
            var terminators = scope.ServiceProvider.GetServices<IShellTerminator>().ToList();
            var registrations = scope.ServiceProvider.GetServices<ShellTerminatorRegistration>().ToList();

            if (terminators.Count == 0 && registrations.Count == 0)
            {
                await scope.DisposeAsync().ConfigureAwait(false);
                return [];
            }

            plan = new ShellTerminatorOrderingPlanner().Plan(_shell.Descriptor, terminators, registrations);
        }
        catch (Exception ex)
        {
            // A broken ordering plan means execution order cannot be trusted; running
            // terminators in arbitrary order would violate the guarantee they exist for.
            // Drain must never fail, so skip them and proceed to disposal.
            _logger.LogError(ex, "Shell terminator planning failed for shell {Shell}; skipping terminators.", _shell.Descriptor);
            await scope.DisposeAsync().ConfigureAwait(false);
            return [];
        }

        foreach (var diagnostic in plan.Diagnostics)
        {
            _logger.LogDebug(
                "{Message} Shell: {Shell}. Terminators: {Terminators}",
                diagnostic.Message,
                _shell.Descriptor,
                string.Join(", ", diagnostic.TerminatorTypes.Select(t => t.FullName ?? t.Name)));
        }

        // Terminators share one cancellation budget: the remaining drain deadline with a
        // grace-period floor (scope-wait and handlers may have consumed the deadline). After a
        // force, a fresh grace-only budget deliberately NOT linked to the already-cancelled
        // force token — force-drain still gives flush work one bounded chance. Under an
        // unbounded policy the budget never fires and ForceAsync remains the escape hatch.
        var budgetCts = new CancellationTokenSource();
        CancellationTokenSource? combined = null;
        if (_cancelSource.IsCancellationRequested)
        {
            budgetCts.CancelAfter(_gracePeriod);
        }
        else
        {
            if (_deadline is { } deadline)
            {
                var remaining = deadline - DateTimeOffset.UtcNow;
                budgetCts.CancelAfter(remaining > _gracePeriod ? remaining : _gracePeriod);
            }

            combined = CancellationTokenSource.CreateLinkedTokenSource(budgetCts.Token, _cancelSource.Token);
        }

        var token = combined?.Token ?? budgetCts.Token;

        var entries = plan.Entries;
        var results = new ShellTerminatorResult[entries.Count];
        // Pre-seed with "not completed" defaults so abandoned terminators leave a valid entry.
        for (var i = 0; i < entries.Count; i++)
            results[i] = new ShellTerminatorResult(entries[i].TerminatorType.Name, Completed: false, Elapsed: TimeSpan.Zero, Error: null);

        // Sequential invocation in mirror-reversed lifecycle order; log-and-continue per
        // terminator so one failure never blocks the rest of the teardown.
        var loop = Task.Run(async () =>
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var sw = Stopwatch.StartNew();
                try
                {
                    await entry.Terminator.TerminateAsync(token).ConfigureAwait(false);
                    sw.Stop();
                    results[i] = new ShellTerminatorResult(entry.TerminatorType.Name, Completed: true, sw.Elapsed, Error: null);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    sw.Stop();
                    results[i] = results[i] with { Elapsed = sw.Elapsed };
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _logger.LogWarning(ex, "Shell terminator {Terminator} threw for shell {Shell}", entry.TerminatorType.FullName, _shell.Descriptor);
                    results[i] = new ShellTerminatorResult(entry.TerminatorType.Name, Completed: false, sw.Elapsed, Error: ex);
                }
            }
        });

        // Defer disposal of the scope and cancellation sources until the loop actually
        // finishes, even if it is abandoned below — disposing them while a terminator is
        // mid-flight would yield use-after-dispose for services resolved into the scope.
        _ = loop.ContinueWith(
            async _ =>
            {
                try
                {
                    await scope.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Terminator scope disposal failed for shell {Shell}", _shell.Descriptor);
                }
                combined?.Dispose();
                budgetCts.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        // Wait for the loop to complete, or cancellation + grace to elapse. A terminator still
        // running after grace is abandoned; disposal proceeds (same accepted residual hazard
        // as abandoned drain handlers).
        if (!loop.IsCompleted)
            await Task.WhenAny(loop, WaitForCancellationThenGrace(token)).ConfigureAwait(false);

        if (!loop.IsCompleted)
        {
            _logger.LogWarning(
                "Shell termination for {Shell} abandoned after cancellation and grace; {Remaining} terminator(s) did not complete.",
                _shell.Descriptor,
                results.Count(r => !r.Completed));
        }

        return results.ToList();
    }

    private async Task WaitForCancellationThenGrace(CancellationToken token)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation (deadline or force) fired; give handlers the grace period to wrap up.
            await Task.Delay(_gracePeriod).ConfigureAwait(false);
        }
    }

    private DrainStatus ResolveStatus(IReadOnlyList<DrainHandlerResult> results)
    {
        if (Volatile.Read(ref _force) == 1)
            return DrainStatus.Forced;

        // If any handler did not complete, deadline was breached.
        if (results.Any(r => !r.Completed))
            return DrainStatus.TimedOut;

        return DrainStatus.Completed;
    }
}
