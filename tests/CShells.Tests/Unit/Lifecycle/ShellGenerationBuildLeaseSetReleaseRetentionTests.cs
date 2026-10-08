using System.Runtime.CompilerServices;
using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Unit.Lifecycle;

public sealed class ShellGenerationBuildLeaseSetReleaseRetentionTests
{
    [Fact]
    public async Task FailedReleaseDoesNotRetainExceptionGraphOrRetryUnresolvedLease()
    {
        var retained = await CaptureConcurrentFailureAsync();

        CollectGarbage();

        Assert.False(retained.Provider.TryGetTarget(out _));
        Assert.False(retained.Failure.TryGetTarget(out _));
        Assert.Equal(1, retained.LeaseSet.UnresolvedLeaseCount);

        var repeatedFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => retained.LeaseSet.DisposeAsync().AsTask());

        Assert.Contains("previously failed", repeatedFailure.Message);
        Assert.Null(repeatedFailure.InnerException);
        Assert.Equal(1, retained.Lease.DisposeCount);
        Assert.Equal(1, retained.LeaseSet.UnresolvedLeaseCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<RetainedRelease> CaptureConcurrentFailureAsync()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var providerReference = new WeakReference<object>(provider);
        var failure = new ProviderHoldingException(provider);
        var failureReference = new WeakReference<Exception>(failure);
        var lease = new GatedFailureLease(failureReference);
        var leaseSet = new ShellGenerationBuildLeaseSet(ShellDescriptor.Create("retained", 1));
        leaseSet.Add(lease);
        await provider.DisposeAsync();

        var firstAttempt = CaptureReleaseFailureAsync(leaseSet.DisposeAsync());
        await lease.DisposeStarted.WaitAsync(TimeSpan.FromSeconds(5));
        var concurrentAttempt = CaptureReleaseFailureAsync(leaseSet.DisposeAsync());
        Assert.False(firstAttempt.IsCompleted);

        lease.Release();
        var firstObserved = await firstAttempt.WaitAsync(TimeSpan.FromSeconds(5));
        var concurrentObserved = await concurrentAttempt.WaitAsync(TimeSpan.FromSeconds(5));
        var firstFailure = Assert.IsType<ProviderHoldingException>(Assert.Single(firstObserved.InnerExceptions));

        Assert.Same(failure, firstFailure);
        Assert.Same(failure, Assert.Single(concurrentObserved.InnerExceptions));
        Assert.Same(provider, firstFailure.Provider);
        Assert.Same(provider, firstFailure.Data["provider"]);
        Assert.Equal(1, lease.DisposeCount);
        Assert.Equal(1, leaseSet.UnresolvedLeaseCount);

        return new RetainedRelease(leaseSet, lease, providerReference, failureReference);
    }

    private static async Task<AggregateException> CaptureReleaseFailureAsync(ValueTask release)
    {
        try
        {
            await release;
            throw new InvalidOperationException("The lease release unexpectedly succeeded.");
        }
        catch (AggregateException exception)
        {
            return exception;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CollectGarbage()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private sealed record RetainedRelease(
        ShellGenerationBuildLeaseSet LeaseSet,
        GatedFailureLease Lease,
        WeakReference<object> Provider,
        WeakReference<Exception> Failure);

    private sealed class ProviderHoldingException : Exception
    {
        public ProviderHoldingException(object provider) : base("lease release failed")
        {
            Provider = provider;
            Data["provider"] = provider;
        }

        public object Provider { get; }
    }

    private sealed class GatedFailureLease(WeakReference<Exception> failure) : IShellGenerationBuildLease
    {
        private readonly TaskCompletionSource disposeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int disposeCount;

        public Task DisposeStarted => disposeStarted.Task;

        public int DisposeCount => Volatile.Read(ref disposeCount);

        public void Release() => release.TrySetResult();

        public ValueTask OnSnapshotSelectedAsync(
            CShells.Features.RuntimeFeatureCatalogSnapshot snapshot,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public async ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref disposeCount);
            disposeStarted.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (!failure.TryGetTarget(out var exception))
                throw new InvalidOperationException("The test failure exception was collected before release.");

            throw exception;
        }
    }
}
