using CShells.Features;

namespace CShells.Lifecycle;

/// <summary>Owns leases acquired for one generation and releases each at most once.</summary>
internal sealed class ShellGenerationBuildLeaseSet(ShellDescriptor descriptor)
{
    private readonly object releaseGate = new();
    private readonly List<IShellGenerationBuildLease> leases = [];
    private Task? releaseTask;
    private bool releaseCompleted;
    private bool releaseFailed;
    private int unresolvedLeaseCount;

    internal ShellDescriptor Descriptor { get; } = Guard.Against.Null(descriptor);

    internal int UnresolvedLeaseCount => Volatile.Read(ref unresolvedLeaseCount);

    internal void Add(IShellGenerationBuildLease lease)
    {
        lock (releaseGate)
        {
            if (releaseTask is not null || releaseCompleted)
                throw new InvalidOperationException($"Build leases for shell '{Descriptor}' are already being released.");

            leases.Add(Guard.Against.Null(lease));
            Interlocked.Increment(ref unresolvedLeaseCount);
        }
    }

    internal async ValueTask OnSnapshotSelectedAsync(
        RuntimeFeatureCatalogSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        Guard.Against.Null(snapshot);

        foreach (var lease in leases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await lease.OnSnapshotSelectedAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
    }

    internal ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        lock (releaseGate)
        {
            if (releaseTask is not null)
                return new ValueTask(releaseTask);

            if (releaseCompleted)
            {
                return releaseFailed
                    ? ValueTask.FromException(new InvalidOperationException(
                        $"Build lease release for shell '{Descriptor}' previously failed; unresolved leases remain."))
                    : ValueTask.CompletedTask;
            }

            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            releaseTask = completion.Task;
        }

        _ = ReleaseCoreAsync(completion);
        return new ValueTask(completion.Task);
    }

    private async Task ReleaseCoreAsync(TaskCompletionSource completion)
    {
        List<IShellGenerationBuildLease> failedLeases = [];
        List<Exception> failures = [];

        for (var index = leases.Count - 1; index >= 0; index--)
        {
            var lease = leases[index];
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
        leases.Clear();
        leases.AddRange(failedLeases);
        Volatile.Write(ref unresolvedLeaseCount, failedLeases.Count);

        if (failures.Count == 0)
            completion.TrySetResult();
        else
            completion.TrySetException(new AggregateException(
                $"One or more build leases for shell '{Descriptor}' failed to release.",
                failures));

        lock (releaseGate)
        {
            releaseFailed = failures.Count != 0;
            releaseCompleted = true;
            releaseTask = null;
        }
    }
}
