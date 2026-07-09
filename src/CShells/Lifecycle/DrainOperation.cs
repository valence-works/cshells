using System.Diagnostics;
using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Lifecycle;

/// <summary>
/// Default <see cref="IDrainOperation"/>. Coordinates three drain phases:
/// (1) scope wait bounded by the deadline; (2) parallel handler invocation; (3) grace after
/// deadline or force. Exposes itself as <see cref="IDrainExtensionHandle"/> to handlers,
/// delegating extension requests to the configured <see cref="IDrainPolicy"/>.
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

        // Drain may have already completed (status is not Pending) — the CTS is disposed in
        // that case and calling Cancel() would throw ObjectDisposedException. Checking status
        // first makes ForceAsync a clean no-op after completion.
        if (Volatile.Read(ref _status) != (int)DrainStatus.Pending)
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
        try
        {
            // Phase 1: scope wait.
            var scopeWaitStart = Stopwatch.GetTimestamp();
            var abandonedScopes = await AwaitScopeReleaseAsync().ConfigureAwait(false);
            var scopeWaitElapsed = Stopwatch.GetElapsedTime(scopeWaitStart);

            // Phase 2: handler invocation. Linked CTS: deadline or force.
            var handlerResults = await InvokeHandlersAsync().ConfigureAwait(false);

            // Determine overall status.
            var status = ResolveStatus(handlerResults);
            Volatile.Write(ref _status, (int)status);

            return new DrainResult(_shell.Descriptor, status, scopeWaitElapsed, abandonedScopes, handlerResults);
        }
        finally
        {
            // Transition to Drained, then to Disposed (disposes provider) — in a finally so a
            // faulted drain phase (e.g. IDrainHandler resolution failure) cannot park the
            // generation in Draining forever. Disposed-keyed cleanup (endpoint removal,
            // middleware pipeline removal) depends on this transition always firing.
            await _shell.ForceAdvanceAsync(ShellLifecycleState.Drained).ConfigureAwait(false);
            await _shell.DisposeAsync().ConfigureAwait(false);
        }
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
