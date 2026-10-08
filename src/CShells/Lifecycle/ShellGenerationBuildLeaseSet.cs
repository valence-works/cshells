using CShells.Features;

namespace CShells.Lifecycle;

/// <summary>Owns leases acquired for one generation and releases each at most once.</summary>
internal sealed class ShellGenerationBuildLeaseSet(ShellDescriptor descriptor)
{
    private readonly List<IShellGenerationBuildLease> _leases = [];
    private Task? _releaseTask;
    private int _unresolvedLeaseCount;

    internal ShellDescriptor Descriptor { get; } = Guard.Against.Null(descriptor);

    internal int UnresolvedLeaseCount => Volatile.Read(ref _unresolvedLeaseCount);

    internal void Add(IShellGenerationBuildLease lease)
    {
        if (Volatile.Read(ref _releaseTask) is not null)
            throw new InvalidOperationException($"Build leases for shell '{Descriptor}' are already being released.");

        _leases.Add(Guard.Against.Null(lease));
        Interlocked.Increment(ref _unresolvedLeaseCount);
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
        var existing = Volatile.Read(ref _releaseTask);
        if (existing is not null)
            return new ValueTask(existing);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var winner = Interlocked.CompareExchange(ref _releaseTask, completion.Task, null);
        if (winner is not null)
            return new ValueTask(winner);

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
        {
            completion.TrySetResult();
            return;
        }

        completion.TrySetException(new AggregateException(
            $"One or more build leases for shell '{Descriptor}' failed to release.",
            failures));
    }
}
