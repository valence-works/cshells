using CShells.Lifecycle;
using Microsoft.Extensions.Logging;

namespace CShells.Hosting;

internal sealed class ShellActivationRun : IShellActivationRun
{
    private readonly IShellRegistry _registry;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly string[] _names;
    private readonly Dictionary<string, int> _indexes;
    private readonly ShellActivationRetryPolicy? _retryPolicy;
    private readonly IShellActivationAttemptObserver? _observer;
    private readonly CancellationTokenSource _lifetime;
    private readonly CancellationToken _runToken;
    private readonly TaskCompletionSource _initialPass = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _stateGate = new();
    private readonly object _lifetimeGate = new();
    private IReadOnlyList<ShellActivationAttemptState> _snapshot;
    private Task _executionTask = Task.CompletedTask;
    private Task? _cancellationTask;
    private bool _lifetimeDisposed;
    private int _stopRequested;

    internal ShellActivationRun(
        IShellRegistry registry,
        TimeProvider timeProvider,
        ILogger logger,
        IReadOnlyList<string> names,
        ShellActivationRetryPolicy? retryPolicy,
        IShellActivationAttemptObserver? observer,
        CancellationToken startupCancellationToken)
    {
        _registry = Guard.Against.Null(registry);
        _timeProvider = Guard.Against.Null(timeProvider);
        _logger = Guard.Against.Null(logger);
        _names = [.. names];
        _retryPolicy = retryPolicy;
        _observer = observer;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(startupCancellationToken);
        _runToken = _lifetime.Token;
        _indexes = new Dictionary<string, int>(_names.Length, StringComparer.OrdinalIgnoreCase);

        var initialStates = new ShellActivationAttemptState[_names.Length];
        for (var index = 0; index < _names.Length; index++)
        {
            _indexes.Add(_names[index], index);
            initialStates[index] = new ShellActivationAttemptState(_names[index], 0, ShellActivationTargetStatus.Pending);
        }

        _snapshot = Array.AsReadOnly(initialStates);
        if (_names.Length == 0)
        {
            _initialPass.TrySetResult();
            _lifetime.Dispose();
            _lifetimeDisposed = true;
            return;
        }

        _executionTask = Task.Run(RunAsync);
    }

    public Task InitialPass => _initialPass.Task;

    public IReadOnlyList<ShellActivationAttemptState> Snapshot
    {
        get
        {
            if (Volatile.Read(ref _stopRequested) == 0)
            {
                for (var index = 0; index < _names.Length; index++)
                    TryReconcileExternal(index);
            }

            lock (_stateGate)
                return _snapshot;
        }
    }

    public async Task StopAsync(CancellationToken shutdownToken = default)
    {
        RequestStop();
        _ = RequestCancellation();
        await _executionTask.WaitAsync(shutdownToken).ConfigureAwait(false);
    }

    private async Task RunAsync()
    {
        try
        {
            var retrySchedules = new List<RetrySchedule>();
            for (var index = 0; index < _names.Length; index++)
            {
                _runToken.ThrowIfCancellationRequested();
                var retry = await RunAttemptAsync(index).ConfigureAwait(false);
                if (retry is not null)
                    retrySchedules.Add(retry.Value);
            }

            _runToken.ThrowIfCancellationRequested();
            _initialPass.TrySetResult();

            // No timer or retry worker is created until every initial attempt has completed.
            if (retrySchedules.Count > 0 && !_runToken.IsCancellationRequested)
            {
                var retries = retrySchedules.Select(RunRetryLoopAsync).ToArray();
                await Task.WhenAll(retries).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_runToken.IsCancellationRequested)
        {
            _initialPass.TrySetCanceled(_runToken);
            MarkUnsettledTargetsStopped();
        }
        catch (Exception exception)
        {
            _initialPass.TrySetException(exception);
            MarkUnsettledTargetsStopped();
            SafeLog(LogLevel.Error, exception, "The shell activation run stopped after an unexpected runner failure.");
        }
        finally
        {
            await DisposeLifetimeAsync().ConfigureAwait(false);
        }
    }

    private async Task<RetrySchedule?> RunAttemptAsync(int index)
    {
        _runToken.ThrowIfCancellationRequested();
        if (!TryBeginAttempt(index, out var attemptNumber, out var startedAt))
            return null;

        ShellActivationAttempt attempt;
        try
        {
            var returnedShell = await _registry.GetOrActivateAsync(_names[index], _runToken).ConfigureAwait(false);
            var outcome = IsSettledCurrent(returnedShell, _names[index])
                ? ShellActivationAttemptOutcome.Succeeded
                : ShellActivationAttemptOutcome.NotCurrent;
            var completedAt = _timeProvider.GetUtcNow();
            attempt = new ShellActivationAttempt(
                _names[index],
                attemptNumber,
                outcome,
                startedAt,
                completedAt,
                outcome == ShellActivationAttemptOutcome.NotCurrent ? "NotCurrent" : null,
                errorType: null,
                exception: null,
                verifiedGeneration: outcome == ShellActivationAttemptOutcome.Succeeded && returnedShell is Shell settled
                    ? settled.Descriptor.Generation
                    : null)
            {
                ReturnedGeneration = outcome == ShellActivationAttemptOutcome.Succeeded
                    ? returnedShell.Descriptor.Generation
                    : null
            };
        }
        catch (OperationCanceledException) when (_runToken.IsCancellationRequested)
        {
            MarkAttemptCancelled(index, startedAt, _timeProvider.GetUtcNow());
            throw;
        }
        catch (Exception exception)
        {
            var completedAt = _timeProvider.GetUtcNow();
            attempt = new ShellActivationAttempt(
                _names[index],
                attemptNumber,
                ShellActivationAttemptOutcome.ActivationFailed,
                startedAt,
                completedAt,
                "ActivationFailed",
                GetSafeErrorType(exception),
                exception);
        }

        RecordAttempt(attempt);
        await NotifyObserverAsync(attempt).ConfigureAwait(false);

        _runToken.ThrowIfCancellationRequested();
        if (attempt.Outcome == ShellActivationAttemptOutcome.Succeeded || IsSatisfied(index))
            return null;

        if (_retryPolicy is null)
        {
            SetStatus(index, ShellActivationTargetStatus.Failed);
            return null;
        }

        ShellActivationRetryDecision decision;
        try
        {
            decision = _retryPolicy(attempt);
        }
        catch (Exception exception)
        {
            SetPolicyError(index, "RetryPolicyFailed");
            SafeLog(LogLevel.Warning, exception, "The retry policy failed for shell {ShellName}.", _names[index]);
            return null;
        }

        if (IsSatisfied(index))
            return null;

        if (decision.Action == ShellActivationRetryAction.Stop)
        {
            SetStatus(index, ShellActivationTargetStatus.Stopped);
            return null;
        }

        if (decision.Action != ShellActivationRetryAction.RetryAfter || decision.Delay <= TimeSpan.Zero)
        {
            SetPolicyError(index, "InvalidRetryDecision");
            return null;
        }

        SetRetryPending(index, decision.Delay);
        return new RetrySchedule(index, decision.Delay);
    }

    private async Task RunRetryLoopAsync(RetrySchedule schedule)
    {
        var current = schedule;
        while (true)
        {
            _runToken.ThrowIfCancellationRequested();
            if (IsStoppedOrSatisfied(current.Index))
                return;

            if (TryReconcileExternal(current.Index))
                return;

            DateTimeOffset nextAttemptAt;
            try
            {
                nextAttemptAt = _timeProvider.GetUtcNow().Add(current.Delay);
            }
            catch (ArgumentOutOfRangeException)
            {
                SetPolicyError(current.Index, "RetryDeadlineOverflow");
                return;
            }

            if (!SetRetryDeadline(current.Index, nextAttemptAt))
                return;

            try
            {
                await Task.Delay(current.Delay, _timeProvider, _runToken).ConfigureAwait(false);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                SetPolicyError(current.Index, "RetryDelayOutOfRange");
                SafeLog(LogLevel.Warning, exception, "The retry delay is outside the supported range for shell {ShellName}.", _names[current.Index]);
                return;
            }
            catch (OperationCanceledException) when (_runToken.IsCancellationRequested)
            {
                MarkUnsettledTargetsStopped();
                return;
            }

            _runToken.ThrowIfCancellationRequested();
            if (IsStoppedOrSatisfied(current.Index))
                return;

            if (TryReconcileExternal(current.Index))
                return;

            var next = await RunAttemptAsync(current.Index).ConfigureAwait(false);
            if (next is null)
                return;

            current = next.Value;
        }
    }

    private bool TryBeginAttempt(int index, out int attemptNumber, out DateTimeOffset startedAt)
    {
        startedAt = _timeProvider.GetUtcNow();
        lock (_stateGate)
        {
            var previous = _snapshot[index];
            if (Volatile.Read(ref _stopRequested) != 0 || _runToken.IsCancellationRequested
                || IsSatisfiedStatus(previous.Status) || previous.Status == ShellActivationTargetStatus.Stopped)
            {
                attemptNumber = previous.AttemptCount;
                return false;
            }

            attemptNumber = previous.AttemptCount + 1;
            ReplaceState(index, previous with
            {
                AttemptCount = attemptNumber,
                Status = ShellActivationTargetStatus.Attempting,
                NextAttemptAt = null,
                RetryDelay = null,
                PolicyErrorCode = null
            });
            return true;
        }
    }

    private void MarkAttemptCancelled(int index, DateTimeOffset startedAt, DateTimeOffset completedAt)
        => UpdateState(index, previous => previous.Status is ShellActivationTargetStatus.Succeeded or ShellActivationTargetStatus.SatisfiedExternally
            ? previous
            : previous with
            {
                Status = Volatile.Read(ref _stopRequested) != 0
                    ? ShellActivationTargetStatus.Stopped
                    : ShellActivationTargetStatus.Failed,
                LastAttemptStartedAt = startedAt,
                LastAttemptCompletedAt = completedAt,
                NextAttemptAt = null,
                RetryDelay = null,
                ErrorCode = "Cancelled",
                ErrorType = typeof(OperationCanceledException).FullName
            });

    private void RecordAttempt(ShellActivationAttempt attempt)
    {
        var index = _indexes[attempt.ShellName];
        UpdateState(index, previous =>
        {
            var status = previous.Status switch
            {
                ShellActivationTargetStatus.Succeeded or ShellActivationTargetStatus.SatisfiedExternally
                    or ShellActivationTargetStatus.Stopped => previous.Status,
                _ when attempt.Outcome == ShellActivationAttemptOutcome.Succeeded => ShellActivationTargetStatus.Succeeded,
                _ when Volatile.Read(ref _stopRequested) != 0 => ShellActivationTargetStatus.Stopped,
                _ => ShellActivationTargetStatus.Failed
            };

            return previous with
            {
                Status = status,
                FailedAttemptCount = previous.FailedAttemptCount + (attempt.Outcome == ShellActivationAttemptOutcome.Succeeded ? 0 : 1),
                FirstFailedAt = attempt.Outcome == ShellActivationAttemptOutcome.Succeeded ? previous.FirstFailedAt : previous.FirstFailedAt ?? attempt.CompletedAt,
                LastFailedAt = attempt.Outcome == ShellActivationAttemptOutcome.Succeeded ? previous.LastFailedAt : attempt.CompletedAt,
                VerifiedGeneration = IsSatisfiedStatus(previous.Status)
                    ? previous.VerifiedGeneration
                    : attempt.VerifiedGeneration ?? previous.VerifiedGeneration,
                ReturnedGeneration = attempt.ReturnedGeneration ?? previous.ReturnedGeneration,
                LastOutcome = attempt.Outcome,
                LastAttemptStartedAt = attempt.StartedAt,
                LastAttemptCompletedAt = attempt.CompletedAt,
                NextAttemptAt = null,
                ErrorCode = attempt.ErrorCode,
                ErrorType = attempt.ErrorType,
                PolicyErrorCode = null
            };
        });
    }

    private async Task NotifyObserverAsync(ShellActivationAttempt attempt)
    {
        if (_observer is null)
            return;

        try
        {
            await _observer.OnAttemptCompletedAsync(attempt, _runToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_runToken.IsCancellationRequested)
        {
            // Run cancellation is handled immediately after the observer returns.
        }
        catch (Exception exception)
        {
            SafeLog(LogLevel.Warning, exception, "An activation attempt observer failed for shell {ShellName}.", attempt.ShellName);
        }
    }

    private Shell? GetSettledExternalShell(string name)
    {
        try
        {
            var current = _registry.GetActive(name);
            return current is Shell shell
                && shell.IsActivationCommitted
                && ReferenceEquals(current, _registry.GetActive(name))
                    ? shell
                    : null;
        }
        catch
        {
            // Snapshot reconciliation is best-effort; custom registry read failures do not alter state.
            return null;
        }
    }

    private bool IsSettledCurrent(IShell returnedShell, string name)
    {
        var current = _registry.GetActive(name);
        return ReferenceEquals(returnedShell, current)
            && (returnedShell is not Shell shell || shell.IsActivationCommitted);
    }

    private bool TryReconcileExternal(int index)
    {
        if (!CanReconcileExternal(index))
            return false;

        var shell = GetSettledExternalShell(_names[index]);
        if (shell is null)
            return false;

        lock (_stateGate)
        {
            var previous = _snapshot[index];
            if (Volatile.Read(ref _stopRequested) != 0 || _runToken.IsCancellationRequested
                || previous.Status is ShellActivationTargetStatus.Succeeded or ShellActivationTargetStatus.SatisfiedExternally)
                return IsSatisfiedStatus(previous.Status);

            ReplaceState(index, previous with
            {
                Status = ShellActivationTargetStatus.SatisfiedExternally,
                VerifiedGeneration = shell.Descriptor.Generation,
                NextAttemptAt = null,
                RetryDelay = null,
                PolicyErrorCode = null
            });
            return true;
        }
    }

    private bool CanReconcileExternal(int index)
    {
        lock (_stateGate)
        {
            var status = _snapshot[index].Status;
            return Volatile.Read(ref _stopRequested) == 0 && !_runToken.IsCancellationRequested
                && status is not (ShellActivationTargetStatus.Succeeded or ShellActivationTargetStatus.SatisfiedExternally);
        }
    }

    private void SetRetryPending(int index, TimeSpan delay)
        => UpdateState(index, previous => previous.Status is ShellActivationTargetStatus.Succeeded
            or ShellActivationTargetStatus.SatisfiedExternally or ShellActivationTargetStatus.Stopped
                ? previous
                : previous with { Status = ShellActivationTargetStatus.RetryPending, RetryDelay = delay });

    private bool SetRetryDeadline(int index, DateTimeOffset nextAttemptAt)
    {
        lock (_stateGate)
        {
            var previous = _snapshot[index];
            if (Volatile.Read(ref _stopRequested) != 0 || _runToken.IsCancellationRequested
                || IsSatisfiedStatus(previous.Status) || previous.Status == ShellActivationTargetStatus.Stopped)
                return false;

            ReplaceState(index, previous with
            {
                Status = ShellActivationTargetStatus.RetryScheduled,
                NextAttemptAt = nextAttemptAt
            });
            return true;
        }
    }

    private void SetPolicyError(int index, string code)
    {
        UpdateState(index, previous => previous.Status is ShellActivationTargetStatus.Succeeded
            or ShellActivationTargetStatus.SatisfiedExternally or ShellActivationTargetStatus.Stopped
                ? previous
                : previous with
                {
                    Status = ShellActivationTargetStatus.PolicyError,
                    NextAttemptAt = null,
                    RetryDelay = null,
                    PolicyErrorCode = code
                });
    }

    private void SetStatus(int index, ShellActivationTargetStatus status)
        => UpdateState(index, previous => IsSatisfiedStatus(previous.Status)
            ? previous
            : previous with { Status = status, NextAttemptAt = null, RetryDelay = null });

    private bool IsSatisfied(int index)
    {
        lock (_stateGate)
            return _snapshot[index].Status is ShellActivationTargetStatus.Succeeded or ShellActivationTargetStatus.SatisfiedExternally;
    }

    private bool IsStoppedOrSatisfied(int index)
    {
        lock (_stateGate)
            return Volatile.Read(ref _stopRequested) != 0 || IsTerminalStatus(_snapshot[index].Status);
    }

    private void MarkUnsettledTargetsStopped()
    {
        lock (_stateGate)
            MarkUnsettledTargetsStoppedCore();
    }

    private void RequestStop()
    {
        lock (_stateGate)
        {
            Interlocked.Exchange(ref _stopRequested, 1);
            MarkUnsettledTargetsStoppedCore();
        }
    }

    private void MarkUnsettledTargetsStoppedCore()
    {
        var next = _snapshot.ToArray();
        var changed = false;
        for (var index = 0; index < next.Length; index++)
        {
            if (next[index].Status is ShellActivationTargetStatus.Pending
                or ShellActivationTargetStatus.Attempting
                or ShellActivationTargetStatus.RetryPending
                or ShellActivationTargetStatus.RetryScheduled)
            {
                next[index] = next[index] with { Status = ShellActivationTargetStatus.Stopped, NextAttemptAt = null, RetryDelay = null };
                changed = true;
            }
        }

        if (changed)
            _snapshot = Array.AsReadOnly(next);
    }

    private void UpdateState(int index, Func<ShellActivationAttemptState, ShellActivationAttemptState> update)
    {
        lock (_stateGate)
            ReplaceState(index, update(_snapshot[index]));
    }

    private void ReplaceState(int index, ShellActivationAttemptState state)
    {
        var next = _snapshot.ToArray();
        next[index] = state;
        _snapshot = Array.AsReadOnly(next);
    }

    private Task RequestCancellation()
    {
        lock (_lifetimeGate)
        {
            if (_lifetimeDisposed)
                return _cancellationTask ?? Task.CompletedTask;

            return _cancellationTask ??= _lifetime.CancelAsync();
        }
    }

    private async Task DisposeLifetimeAsync()
    {
        Task? cancellationTask;
        lock (_lifetimeGate)
        {
            if (_lifetimeDisposed)
                return;

            _lifetimeDisposed = true;
            cancellationTask = _cancellationTask;
        }

        if (cancellationTask is not null)
        {
            try
            {
                await cancellationTask.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                SafeLog(LogLevel.Warning, exception, "A shell activation run cancellation callback failed.");
            }
        }

        _lifetime.Dispose();
    }

    private void SafeLog(LogLevel level, Exception exception, string message, params object?[] args)
    {
        try
        {
            _logger.Log(level, exception, message, args);
        }
        catch
        {
            // Logging is observational and cannot change run state or task ownership.
        }
    }

    private static string GetSafeErrorType(Exception exception)
        => exception.GetType().FullName ?? exception.GetType().Name;

    private static bool IsSatisfiedStatus(ShellActivationTargetStatus status)
        => status is ShellActivationTargetStatus.Succeeded or ShellActivationTargetStatus.SatisfiedExternally;

    private static bool IsTerminalStatus(ShellActivationTargetStatus status)
        => status is ShellActivationTargetStatus.Succeeded
            or ShellActivationTargetStatus.SatisfiedExternally
            or ShellActivationTargetStatus.Failed
            or ShellActivationTargetStatus.Stopped
            or ShellActivationTargetStatus.PolicyError;

    private readonly record struct RetrySchedule(int Index, TimeSpan Delay);
}
