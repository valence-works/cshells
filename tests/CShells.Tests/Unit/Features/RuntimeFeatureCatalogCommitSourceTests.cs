using System.Reflection;
using CShells.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Tests.Unit.Features;

public class RuntimeFeatureCatalogCommitSourceTests
{
    [Fact]
    public async Task RefreshAsync_NotifiesForInitialAndLaterCommitsWithExactSnapshots()
    {
        var catalog = CreateCatalog();
        var source = (IRuntimeFeatureCatalogCommitSource)new RuntimeFeatureCatalogAccessor(catalog);
        List<RuntimeFeatureCatalogSnapshot> committed = [];
        List<long> currentGenerationsSeenByCallbacks = [];
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
        List<RuntimeFeatureCatalogSnapshot> committed = [];
        source.SnapshotCommitted += committed.Add;

        var next = await catalog.RefreshAsync();

        Assert.Equal([next], committed);
    }

    [Fact]
    public async Task RefreshAsync_IsolatesSubscriberFailuresAndHonorsUnsubscription()
    {
        var catalog = CreateCatalog();
        var source = (IRuntimeFeatureCatalogCommitSource)new RuntimeFeatureCatalogAccessor(catalog);
        List<long> received = [];
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
        List<RuntimeFeatureCatalogSnapshot> committed = [];
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
        List<long> received = [];
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
        List<long> received = [];
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
            List<long> received = [];
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

    [Fact]
    public async Task RefreshAsync_LateSubscriberDoesNotReceiveAlreadyCommittedQueuedGeneration()
    {
        await using var blocked = new BlockedGenerationNotifications();
        List<long> lateGenerations = [];
        Action<RuntimeFeatureCatalogSnapshot> lateSubscriber = snapshot => lateGenerations.Add(snapshot.Generation);

        await blocked.StartFirstRefreshAsync();
        var second = await blocked.CommitSecondWhileBlockedAsync();
        Assert.Equal(2, second.Generation);
        Assert.Equal(1, Assert.Single(blocked.BlockingSubscriberGenerations));
        blocked.Source.SnapshotCommitted += lateSubscriber;
        await blocked.ReleaseAndJoinAsync();

        var third = await blocked.Catalog.RefreshAsync();

        Assert.Equal([1L, 2L, 3L], blocked.BlockingSubscriberGenerations);
        Assert.Equal([third.Generation], lateGenerations);
        Assert.Equal(3, third.Generation);
    }

    [Fact]
    public async Task RefreshAsync_UnsubscribeBeforeQueuedGenerationSamplingSuppressesOnlyQueuedDelivery()
    {
        await using var blocked = new BlockedGenerationNotifications();
        List<long> targetGenerations = [];
        Action<RuntimeFeatureCatalogSnapshot> targetSubscriber = snapshot => targetGenerations.Add(snapshot.Generation);
        blocked.Source.SnapshotCommitted += targetSubscriber;

        await blocked.StartFirstRefreshAsync();
        var second = await blocked.CommitSecondWhileBlockedAsync();
        Assert.Equal(2, second.Generation);
        blocked.Source.SnapshotCommitted -= targetSubscriber;
        await blocked.ReleaseAndJoinAsync();

        await blocked.Catalog.RefreshAsync();

        Assert.Equal([1L], targetGenerations);
    }

    [Fact]
    public async Task RefreshAsync_EventAddAndRemovePreserveMulticastSubsequenceSemantics()
    {
        var duplicateCatalog = CreateCatalog();
        var duplicateSource = (IRuntimeFeatureCatalogCommitSource)new RuntimeFeatureCatalogAccessor(duplicateCatalog);
        List<string> duplicateCalls = [];
        Action<RuntimeFeatureCatalogSnapshot> first = _ => duplicateCalls.Add("first");
        Action<RuntimeFeatureCatalogSnapshot> second = _ => duplicateCalls.Add("second");
        duplicateSource.SnapshotCommitted += first + second + first;
        duplicateSource.SnapshotCommitted -= first;

        await duplicateCatalog.RefreshAsync();

        Assert.Equal(["first", "second"], duplicateCalls);

        var subsequenceCatalog = CreateCatalog();
        var subsequenceSource = (IRuntimeFeatureCatalogCommitSource)new RuntimeFeatureCatalogAccessor(subsequenceCatalog);
        List<string> subsequenceCalls = [];
        Action<RuntimeFeatureCatalogSnapshot> firstSubsequenceHandler = _ => subsequenceCalls.Add("first");
        Action<RuntimeFeatureCatalogSnapshot> secondSubsequenceHandler = _ => subsequenceCalls.Add("second");
        subsequenceSource.SnapshotCommitted += firstSubsequenceHandler;
        subsequenceSource.SnapshotCommitted += secondSubsequenceHandler;
        subsequenceSource.SnapshotCommitted -= firstSubsequenceHandler + secondSubsequenceHandler;

        await subsequenceCatalog.RefreshAsync();

        Assert.Empty(subsequenceCalls);
    }

    [Fact]
    public async Task RefreshAsync_ThrowingInformationLoggerDoesNotSuppressSubscribersOrCommits()
    {
        var logger = new ThrowingLogger(LogLevel.Information);
        var catalog = new RuntimeFeatureCatalog(_ => Task.FromResult<IReadOnlyCollection<Assembly>>([]), logger);
        var source = (IRuntimeFeatureCatalogCommitSource)new RuntimeFeatureCatalogAccessor(catalog);
        List<long> received = [];
        source.SnapshotCommitted += snapshot => received.Add(snapshot.Generation);
        source.SnapshotCommitted += snapshot => received.Add(snapshot.Generation);

        var first = await catalog.RefreshAsync();
        var second = await catalog.RefreshAsync();

        Assert.Same(second, catalog.CurrentSnapshot);
        Assert.Equal([1L, 2L], [first.Generation, second.Generation]);
        Assert.Equal([1L, 1L, 2L, 2L], received);
        Assert.Equal(2, logger.ThrowCount);
    }

    [Fact]
    public async Task RefreshAsync_ThrowingErrorLoggerDoesNotStopFanOutOrLaterGenerations()
    {
        var logger = new ThrowingLogger(LogLevel.Error);
        var catalog = new RuntimeFeatureCatalog(_ => Task.FromResult<IReadOnlyCollection<Assembly>>([]), logger);
        var source = (IRuntimeFeatureCatalogCommitSource)new RuntimeFeatureCatalogAccessor(catalog);
        List<long> received = [];
        source.SnapshotCommitted += _ => throw new InvalidOperationException("subscriber failed");
        source.SnapshotCommitted += snapshot => received.Add(snapshot.Generation);

        var first = await catalog.RefreshAsync();
        var second = await catalog.RefreshAsync();

        Assert.Same(second, catalog.CurrentSnapshot);
        Assert.Equal([first.Generation, second.Generation], received);
        Assert.Equal(2, logger.ThrowCount);
    }

    private static RuntimeFeatureCatalog CreateCatalog() => new(
        _ => Task.FromResult<IReadOnlyCollection<Assembly>>([]),
        NullLogger<RuntimeFeatureCatalog>.Instance);

    private sealed class BlockedGenerationNotifications : IAsyncDisposable
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);
        private readonly TaskCompletionSource firstSubscriberEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseFirstSubscriber = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task<RuntimeFeatureCatalogSnapshot>? firstRefresh;
        private Task<RuntimeFeatureCatalogSnapshot>? secondRefresh;

        public BlockedGenerationNotifications()
        {
            Catalog = CreateCatalog();
            Source = new RuntimeFeatureCatalogAccessor(Catalog);
            Source.SnapshotCommitted += snapshot =>
            {
                BlockingSubscriberGenerations.Add(snapshot.Generation);
                if (snapshot.Generation != 1)
                    return;

                firstSubscriberEntered.SetResult();
                releaseFirstSubscriber.Task.GetAwaiter().GetResult();
            };
        }

        public RuntimeFeatureCatalog Catalog { get; }

        public IRuntimeFeatureCatalogCommitSource Source { get; }

        public List<long> BlockingSubscriberGenerations { get; } = [];

        public async Task StartFirstRefreshAsync()
        {
            firstRefresh = Task.Run(() => Catalog.RefreshAsync());
            await firstSubscriberEntered.Task.WaitAsync(WaitTimeout);
        }

        public async Task<RuntimeFeatureCatalogSnapshot> CommitSecondWhileBlockedAsync()
        {
            secondRefresh = Task.Run(() => Catalog.RefreshAsync());
            return await secondRefresh.WaitAsync(WaitTimeout);
        }

        public async Task ReleaseAndJoinAsync()
        {
            releaseFirstSubscriber.TrySetResult();
            try
            {
                if (secondRefresh is not null)
                    await secondRefresh.WaitAsync(WaitTimeout);
            }
            finally
            {
                if (firstRefresh is not null)
                    await firstRefresh.WaitAsync(WaitTimeout);
            }
        }

        public ValueTask DisposeAsync() => new(ReleaseAndJoinAsync());
    }

    private sealed class ThrowingLogger(LogLevel throwAt) : ILogger<RuntimeFeatureCatalog>
    {
        public int ThrowCount { get; private set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel != throwAt)
                return;

            ThrowCount++;
            throw new InvalidOperationException("logger failed");
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose() { }
        }
    }
}
