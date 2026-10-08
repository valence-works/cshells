using System.Reflection;
using CShells.Features;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Tests.Unit.Features;

public class RuntimeFeatureCatalogCommitSourceTests
{
    [Fact]
    public async Task RefreshAsync_NotifiesForInitialAndLaterCommitsWithExactSnapshots()
    {
        var catalog = CreateCatalog();
        IRuntimeFeatureCatalogCommitSource source = new RuntimeFeatureCatalogAccessor(catalog);
        var committed = new List<RuntimeFeatureCatalogSnapshot>();
        var currentGenerationsSeenByCallbacks = new List<long>();
        source.SnapshotCommitted += snapshot =>
        {
            currentGenerationsSeenByCallbacks.Add(catalog.CurrentSnapshot.Generation);
            committed.Add(snapshot);
        };

        var first = await catalog.GetSnapshotAsync();
        var same = await catalog.GetSnapshotAsync();
        await catalog.EnsureInitializedAsync();
        var second = await catalog.RefreshAsync();

        Assert.Same(first, same);
        Assert.Equal([first, second], committed);
        Assert.Equal([first.Generation, second.Generation], currentGenerationsSeenByCallbacks);
        Assert.Same(second, catalog.CurrentSnapshot);
    }

    [Fact]
    public async Task RefreshAsync_DoesNotReplayToLateSubscribers()
    {
        var catalog = CreateCatalog();
        await catalog.RefreshAsync();
        var source = (IRuntimeFeatureCatalogCommitSource)new RuntimeFeatureCatalogAccessor(catalog);
        var committed = new List<RuntimeFeatureCatalogSnapshot>();
        source.SnapshotCommitted += committed.Add;

        var next = await catalog.RefreshAsync();

        Assert.Equal([next], committed);
    }

    [Fact]
    public async Task RefreshAsync_IsolatesSubscriberFailuresAndHonorsUnsubscription()
    {
        var catalog = CreateCatalog();
        var source = (IRuntimeFeatureCatalogCommitSource)new RuntimeFeatureCatalogAccessor(catalog);
        var received = new List<long>();
        Action<RuntimeFeatureCatalogSnapshot> throwing = _ => throw new InvalidOperationException("subscriber failed");
        Action<RuntimeFeatureCatalogSnapshot> observer = snapshot => received.Add(snapshot.Generation);
        source.SnapshotCommitted += throwing;
        source.SnapshotCommitted += observer;

        var first = await catalog.RefreshAsync();
        source.SnapshotCommitted -= observer;
        var second = await catalog.RefreshAsync();

        Assert.Equal([first.Generation], received);
        Assert.True(second.Generation > first.Generation);
    }

    [Fact]
    public async Task RefreshAsync_FailureAndPrecommitCancellationDoNotPublishOrNotify()
    {
        var shouldFail = false;
        var shouldCancel = false;
        using var cancellation = new CancellationTokenSource();
        var catalog = new RuntimeFeatureCatalog(
            _ =>
            {
                if (shouldFail)
                    throw new InvalidOperationException("discovery failed");
                if (shouldCancel)
                {
                    cancellation.Cancel();
                    return Task.FromResult<IReadOnlyCollection<Assembly>>([]);
                }
                return Task.FromResult<IReadOnlyCollection<Assembly>>([]);
            },
            NullLogger<RuntimeFeatureCatalog>.Instance);
        var source = (IRuntimeFeatureCatalogCommitSource)new RuntimeFeatureCatalogAccessor(catalog);
        var committed = new List<RuntimeFeatureCatalogSnapshot>();
        source.SnapshotCommitted += committed.Add;
        var initial = await catalog.RefreshAsync();

        shouldFail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.RefreshAsync());
        shouldFail = false;
        shouldCancel = true;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.RefreshAsync(cancellation.Token));

        Assert.Same(initial, catalog.CurrentSnapshot);
        Assert.Equal([initial], committed);
    }

    [Fact]
    public async Task RefreshAsync_CancellationAfterCommitDoesNotUndoNotification()
    {
        using var cancellation = new CancellationTokenSource();
        var catalog = CreateCatalog();
        var source = (IRuntimeFeatureCatalogCommitSource)new RuntimeFeatureCatalogAccessor(catalog);
        RuntimeFeatureCatalogSnapshot? notified = null;
        source.SnapshotCommitted += snapshot =>
        {
            notified = snapshot;
            cancellation.Cancel();
        };

        var committed = await catalog.RefreshAsync(cancellation.Token);

        Assert.Same(committed, notified);
        Assert.Same(committed, catalog.CurrentSnapshot);
    }

    [Fact]
    public async Task RefreshAsync_AllowsReentrantRefreshAndDispatchesInCommitOrder()
    {
        var catalog = CreateCatalog();
        var source = (IRuntimeFeatureCatalogCommitSource)new RuntimeFeatureCatalogAccessor(catalog);
        var received = new List<long>();
        var reentered = false;
        source.SnapshotCommitted += snapshot =>
        {
            received.Add(snapshot.Generation);
            if (!reentered)
            {
                reentered = true;
                catalog.RefreshAsync().GetAwaiter().GetResult();
            }
        };

        var first = await Task.Run(() => catalog.RefreshAsync()).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal([first.Generation, first.Generation + 1], received);
        Assert.Equal(first.Generation + 1, catalog.CurrentSnapshot.Generation);
    }

    [Fact]
    public async Task RefreshAsync_QueuesConcurrentCommitsWhileSubscriberRunsAndDrainsEveryGeneration()
    {
        var catalog = CreateCatalog();
        var source = (IRuntimeFeatureCatalogCommitSource)new RuntimeFeatureCatalogAccessor(catalog);
        var enteredSubscriber = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSubscriber = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new List<long>();
        source.SnapshotCommitted += snapshot =>
        {
            received.Add(snapshot.Generation);
            if (received.Count == 1)
            {
                enteredSubscriber.SetResult(true);
                releaseSubscriber.Task.GetAwaiter().GetResult();
            }
        };

        var firstRefresh = Task.Run(() => catalog.RefreshAsync());
        RuntimeFeatureCatalogSnapshot[] queuedRefreshes;
        try
        {
            await enteredSubscriber.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var refreshTasks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
            {
                await start.Task;
                return await catalog.RefreshAsync();
            })).ToArray();
            start.SetResult(true);
            queuedRefreshes = await Task.WhenAll(refreshTasks).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, Assert.Single(received));
            Assert.True(catalog.CurrentSnapshot.Generation > received[0]);
        }
        finally
        {
            releaseSubscriber.SetResult(true);
        }
        var first = await firstRefresh.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(9, received.Count);
        Assert.Equal(received.Order().ToArray(), received);
        Assert.Equal(Enumerable.Range((int)first.Generation, 9).Select(generation => (long)generation), received);
        Assert.Equal(queuedRefreshes.Max(snapshot => snapshot.Generation), catalog.CurrentSnapshot.Generation);
    }

    [Fact]
    public async Task RefreshAsync_DoesNotLoseCommitAtDispatcherHandoff()
    {
        // Repeated batches exercise the empty-queue/drainer-ownership transition.
        for (var batch = 0; batch < 20; batch++)
        {
            var catalog = CreateCatalog();
            var source = (IRuntimeFeatureCatalogCommitSource)new RuntimeFeatureCatalogAccessor(catalog);
            var received = new List<long>();
            source.SnapshotCommitted += snapshot => received.Add(snapshot.Generation);

            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var refreshTasks = Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
            {
                await start.Task;
                return await catalog.RefreshAsync();
            })).ToArray();
            start.SetResult(true);
            var commits = await Task.WhenAll(refreshTasks).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(commits.Length, received.Count);
            Assert.Equal(commits.Select(snapshot => snapshot.Generation).Order(), received);
        }
    }

    private static RuntimeFeatureCatalog CreateCatalog() => new(
        _ => Task.FromResult<IReadOnlyCollection<Assembly>>([]),
        NullLogger<RuntimeFeatureCatalog>.Instance);
}
