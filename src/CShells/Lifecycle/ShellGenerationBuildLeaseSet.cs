using CShells.Features;

namespace CShells.Lifecycle;

/// <summary>Owns leases acquired for one generation and releases each at most once.</summary>
internal sealed class ShellGenerationBuildLeaseSet(ShellDescriptor descriptor)
{
    private readonly object _releaseGate = new();
    private readonly List<IShellGenerationBuildLease> _leases = [];
    private Task? _releaseTask;
    private bool _releaseCompleted;
    private bool _releaseFailed;
    private int _unresolvedLeaseCount;

    internal ShellDescriptor Descriptor { get; } = Guard.Against.Null(descriptor);

    internal int UnresolvedLeaseCount => Volatile.Read(ref _unresolvedLeaseCount);

    internal void Add(IShellGenerationBuildLease lease)
    {
        lock (_releaseGate)
        {
            if (_releaseTask is not null || _releaseCompleted)
                throw new InvalidOperationException($"Build leases for shell '{Descriptor}' are already being released.");

            _leases.Add(Guard.Against.Null(lease));
            Interlocked.Increment(ref _unresolvedLeaseCount);
        }
    }

    internal async ValueTask OnSnapshotSelectedAsync(
        RuntimeFeatureCatalogSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        Guard.Against.Null(snapshot);

        foreach (var lease in _leases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await lease.OnSnapshotSelectedAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
    }

    internal ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        lock (_releaseGate)
        {
            if (_releaseTask is not null)
                return new ValueTask(_releaseTask);

            if (_releaseCompleted)
            {
                return _releaseFailed
                    ? ValueTask.FromException(new InvalidOperationException(
                        $"Build lease release for shell '{Descriptor}' previously failed; unresolved leases remain."))
                    : ValueTask.CompletedTask;
            }

            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _releaseTask = completion.Task;
        }

        _ = ReleaseCoreAsync(completion);
        return new ValueTask(completion.Task);
    }

    private async Task ReleaseCoreAsync(TaskCompletionSource completion)
    {
        var failedLeases = new List<IShellGenerationBuildLease>();
        var failures = new List<Exception>();

        for (var index = _leases.Count - 1; index >= 0; index--)
        {
            var lease = _leases[index];
            try
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failedLeases.Add(lease);
                failures.Add(exception);
            }
        }

        failedLeases.Reverse();
        _leases.Clear();
        _leases.AddRange(failedLeases);
        Volatile.Write(ref _unresolvedLeaseCount, failedLeases.Count);

        if (failures.Count == 0)
            completion.TrySetResult();
        else
            completion.TrySetException(new AggregateException(
                $"One or more build leases for shell '{Descriptor}' failed to release.",
                failures));

        lock (_releaseGate)
        {
            _releaseFailed = failures.Count != 0;
            _releaseCompleted = true;
            _releaseTask = null;
        }
    }
}
