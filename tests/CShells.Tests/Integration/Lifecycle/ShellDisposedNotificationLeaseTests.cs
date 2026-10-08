using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CShells.Tests.Integration.Lifecycle;

public sealed class ShellDisposedNotificationLeaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryDisposedSubscriberFailureStillNotifiesPeersAndDisposesProviderButRetainsLeases(bool loggerThrows)
    {
        var participant = new RecordingBuildParticipant();
        var failingSubscriber = new ThrowingDisposedSubscriber();
        var peerSubscriber = new RecordingDisposedSubscriber();
        await using var host = ShellRegistryActivateTests.BuildHost(
            cshells => cshells
                .WithAssemblyContaining<ShellDisposedNotificationLeaseTests>()
                .AddShell("disposed-notification", shell => shell.WithFeature<ProviderProbeFeature>()),
            services =>
            {
                services.AddSingleton<IShellGenerationBuildParticipant>(participant);
                services.AddSingleton<IShellLifecycleSubscriber>(failingSubscriber);
                services.AddSingleton<IShellLifecycleSubscriber>(peerSubscriber);
                if (loggerThrows)
                    services.AddSingleton<ILogger<ShellRegistry>, ThrowingErrorLogger<ShellRegistry>>();
            });
        var registry = Assert.IsType<ShellRegistry>(host.GetRequiredService<IShellRegistry>());
        var shell = await registry.ActivateAsync("disposed-notification");
        var probe = shell.ServiceProvider.GetRequiredService<ProviderDisposalProbe>();

        var drain = await registry.DrainAsync(shell);
        await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, failingSubscriber.DisposedNotifications);
        Assert.Equal(1, peerSubscriber.DisposedNotifications);
        Assert.Equal(1, probe.DisposeCount);
        Assert.Equal(0, participant.Lease.DisposeCount);
        Assert.Equal(1, registry.RetainedBuildLeaseCount);
        Assert.Null(registry.GetActive("disposed-notification"));
    }

    [ShellFeature("DisposedNotificationProviderProbe")]
    public sealed class ProviderProbeFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) => services.AddSingleton<ProviderDisposalProbe>();
    }

    public sealed class ProviderDisposalProbe : IDisposable
    {
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }

    private sealed class RecordingBuildParticipant : IShellGenerationBuildParticipant
    {
        public RecordingBuildLease Lease { get; } = new();

        public ValueTask<IShellGenerationBuildLease> BeginAsync(
            ShellGenerationBuildContext context,
            CancellationToken cancellationToken = default) => ValueTask.FromResult<IShellGenerationBuildLease>(Lease);
    }

    private sealed class RecordingBuildLease : IShellGenerationBuildLease
    {
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public ValueTask OnSnapshotSelectedAsync(RuntimeFeatureCatalogSnapshot snapshot, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingDisposedSubscriber : IShellLifecycleSubscriber
    {
        private int _disposedNotifications;

        public int DisposedNotifications => Volatile.Read(ref _disposedNotifications);

        public Task OnStateChangedAsync(
            IShell shell,
            ShellLifecycleState previous,
            ShellLifecycleState current,
            CancellationToken cancellationToken = default)
        {
            if (current != ShellLifecycleState.Disposed)
                return Task.CompletedTask;

            Interlocked.Increment(ref _disposedNotifications);
            throw new InvalidOperationException("disposed subscriber failed");
        }
    }

    private sealed class RecordingDisposedSubscriber : IShellLifecycleSubscriber
    {
        private int _disposedNotifications;

        public int DisposedNotifications => Volatile.Read(ref _disposedNotifications);

        public Task OnStateChangedAsync(
            IShell shell,
            ShellLifecycleState previous,
            ShellLifecycleState current,
            CancellationToken cancellationToken = default)
        {
            if (current == ShellLifecycleState.Disposed)
                Interlocked.Increment(ref _disposedNotifications);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingErrorLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
                throw new InvalidOperationException("logger failed");
        }
    }
}
