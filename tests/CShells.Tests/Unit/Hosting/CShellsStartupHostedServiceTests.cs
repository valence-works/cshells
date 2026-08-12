using CShells.Hosting;
using CShells.Lifecycle;

namespace CShells.Tests.Unit.Hosting;

public class CShellsStartupHostedServiceTests
{
    [Fact(DisplayName = "Concurrent shutdown callers share the same in-flight completion")]
    public async Task StopAsync_ConcurrentCallersShareInFlightShutdown()
    {
        var registry = new ShutdownRaceRegistry();
        var hostedService = new CShellsStartupHostedService(registry);

        var first = hostedService.StopAsync(CancellationToken.None);
        await registry.DrainStarted;

        var second = hostedService.StopAsync(CancellationToken.None);
        registry.CompleteDrain();

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(first, second));

        Assert.Null(exception);
        Assert.Same(first, second);
        Assert.Equal(1, registry.DrainCallCount);
    }

    private sealed class ShutdownRaceRegistry : IShellRegistry
    {
        private readonly FakeShell shell = new();
        private readonly BlockingDrainOperation drain = new();
        private readonly TaskCompletionSource drainStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int drainCallCount;

        public Task DrainStarted => drainStarted.Task;
        public int DrainCallCount => Volatile.Read(ref drainCallCount);

        public Task<IDrainOperation> DrainAsync(IShell candidate, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref drainCallCount) > 1)
                throw new ObjectDisposedException(nameof(IServiceProvider));

            drainStarted.TrySetResult();
            return Task.FromResult<IDrainOperation>(drain);
        }

        public IReadOnlyCollection<IShell> GetActiveShells() => [shell];

        public void CompleteDrain() => drain.Complete(shell.Descriptor);

        public Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IShell> ActivateAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ReloadResult> ReloadAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReloadResult>> ReloadActiveAsync(ReloadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UnregisterBlueprintAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProvidedBlueprint?> GetBlueprintAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IShellBlueprintManager?> GetManagerAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ShellPage> ListAsync(ShellListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IShell? GetActive(string name) => throw new NotSupportedException();
        public IReadOnlyCollection<IShell> GetAll(string name) => throw new NotSupportedException();
        public void Subscribe(IShellLifecycleSubscriber subscriber) => throw new NotSupportedException();
        public void Unsubscribe(IShellLifecycleSubscriber subscriber) => throw new NotSupportedException();
    }

    private sealed class BlockingDrainOperation : IDrainOperation
    {
        private readonly TaskCompletionSource<DrainResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DrainStatus Status => completion.Task.IsCompleted ? DrainStatus.Completed : DrainStatus.Pending;
        public DateTimeOffset? Deadline => null;

        public Task<DrainResult> WaitAsync(CancellationToken cancellationToken = default) =>
            completion.Task.WaitAsync(cancellationToken);

        public Task ForceAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Complete(ShellDescriptor descriptor) =>
            completion.TrySetResult(new DrainResult(descriptor, DrainStatus.Completed, TimeSpan.Zero, 0, []));
    }

    private sealed class FakeShell : IShell
    {
        public ShellDescriptor Descriptor { get; } = ShellDescriptor.Create("test", 1);
        public ShellLifecycleState State => ShellLifecycleState.Active;
        public IServiceProvider ServiceProvider { get; } = new EmptyServiceProvider();
        public IDrainOperation? Drain => null;

        public IShellScope BeginScope() => throw new NotSupportedException();
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
