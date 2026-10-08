using CShells.DependencyInjection;
using CShells.Lifecycle;
using CShells.Lifecycle.Blueprints;
using CShells.Lifecycle.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CShells.Tests.Integration.Lifecycle;

public sealed class ShellRegistryActivationSettlementTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact(DisplayName = "GetOrActivate waits for initial commit and returns the committed candidate")]
    public async Task GetOrActivate_WaitsForInitialCommit_ReturnsCommittedCandidate()
    {
        var participant = new GatedActivationParticipant();
        await using var host = BuildHost(
            cshells => cshells.WithAssemblies().AddShell("settlement", _ => { }),
            services => services.AddSingleton<IShellGenerationActivationParticipant>(participant));
        var registry = host.Services.GetRequiredService<IShellRegistry>();
        Task<IShell>? activation = null;
        PendingRequest? waiter = null;

        try
        {
            activation = Task.Run(() => registry.ActivateAsync("settlement"));
            var committedCandidate = await participant.CommitEntered.WaitAsync(Timeout);

            Assert.Same(committedCandidate, registry.GetActive("settlement"));
            Assert.Contains(committedCandidate, registry.GetAll("settlement"));

            waiter = await StartGetOrActivateAsync(registry, "settlement");
            Assert.False(waiter.Operation.IsCompleted);

            participant.ReleaseCommit();
            var activationResult = await activation.WaitAsync(Timeout);
            var waiterResult = await waiter.Worker.WaitAsync(Timeout);

            Assert.Same(committedCandidate, activationResult);
            Assert.Same(committedCandidate, waiterResult);
            Assert.True(((Shell)waiterResult).IsActivationCommitted);
        }
        finally
        {
            participant.ReleaseCommit();
            await IgnoreFailureAsync(activation);
            await IgnoreFailureAsync(waiter?.Worker);
        }
    }

    [Fact(DisplayName = "GetOrActivate recovers after the first activation commit fails")]
    public async Task GetOrActivate_AfterInitialCommitFailure_RunsSerializedRecovery()
    {
        var participant = new GatedActivationParticipant(failGeneration: 1);
        await using var host = BuildHost(
            cshells => cshells.WithAssemblies().AddShell("settlement", _ => { }),
            services => services.AddSingleton<IShellGenerationActivationParticipant>(participant));
        var registry = host.Services.GetRequiredService<IShellRegistry>();
        Task<IShell>? activation = null;
        PendingRequest? waiter = null;

        try
        {
            activation = Task.Run(() => registry.ActivateAsync("settlement"));
            var rejectedCandidate = await participant.CommitEntered.WaitAsync(Timeout);
            waiter = await StartGetOrActivateAsync(registry, "settlement");
            Assert.False(waiter.Operation.IsCompleted);

            participant.ReleaseCommit();
            var failure = await Assert.ThrowsAsync<ShellGenerationActivationException>(async () => await activation.WaitAsync(Timeout));
            var recovered = await waiter.Worker.WaitAsync(Timeout);

            Assert.Equal(1, failure.Descriptor.Generation);
            Assert.Equal(2, recovered.Descriptor.Generation);
            Assert.NotSame(rejectedCandidate, recovered);
            Assert.True(((Shell)recovered).IsActivationCommitted);
            Assert.Same(recovered, registry.GetActive("settlement"));
        }
        finally
        {
            participant.ReleaseCommit();
            await IgnoreFailureAsync(activation);
            await IgnoreFailureAsync(waiter?.Worker);
        }
    }

    [Fact(DisplayName = "GetOrActivate returns the old committed generation while reload is still composing")]
    public async Task GetOrActivate_DuringReloadComposition_ReturnsExistingCommittedGeneration()
    {
        var blueprint = new GatedComposeBlueprint("settlement", gateOnCompose: 2);
        await using var host = BuildHost(cshells => cshells.WithAssemblies().AddBlueprint(blueprint));
        var registry = host.Services.GetRequiredService<IShellRegistry>();
        var original = await registry.ActivateAsync("settlement");
        Task<ReloadResult>? reload = null;

        try
        {
            reload = Task.Run(() => registry.ReloadAsync("settlement"));
            await blueprint.ComposeEntered.WaitAsync(Timeout);

            var request = registry.GetOrActivateAsync("settlement");
            Assert.True(request.IsCompletedSuccessfully);
            Assert.Same(original, await request);

            blueprint.ReleaseCompose();
            var result = await reload.WaitAsync(Timeout);
            Assert.Null(result.Error);
            Assert.NotNull(result.NewShell);
            await result.Drain!.WaitAsync().WaitAsync(Timeout);
        }
        finally
        {
            blueprint.ReleaseCompose();
            await IgnoreFailureAsync(reload);
        }
    }

    [Fact(DisplayName = "GetOrActivate waits for a reload commit and returns the new generation")]
    public async Task GetOrActivate_WaitsForReloadCommit_ReturnsNewGeneration()
    {
        var participant = new GatedActivationParticipant(gatedGeneration: 2);
        await using var host = BuildHost(
            cshells => cshells.WithAssemblies().AddShell("settlement", _ => { }),
            services => services.AddSingleton<IShellGenerationActivationParticipant>(participant));
        var registry = host.Services.GetRequiredService<IShellRegistry>();
        var original = await registry.ActivateAsync("settlement");
        Task<ReloadResult>? reload = null;
        PendingRequest? waiter = null;

        try
        {
            reload = Task.Run(() => registry.ReloadAsync("settlement"));
            var candidate = await participant.CommitEntered.WaitAsync(Timeout);
            Assert.Equal(2, candidate.Descriptor.Generation);
            Assert.Same(candidate, registry.GetActive("settlement"));

            waiter = await StartGetOrActivateAsync(registry, "settlement");
            Assert.False(waiter.Operation.IsCompleted);

            participant.ReleaseCommit();
            var result = await reload.WaitAsync(Timeout);
            var waiterResult = await waiter.Worker.WaitAsync(Timeout);

            Assert.Null(result.Error);
            Assert.Same(candidate, result.NewShell);
            Assert.Same(candidate, waiterResult);
            await result.Drain!.WaitAsync().WaitAsync(Timeout);
            Assert.Equal(ShellLifecycleState.Disposed, original.State);
        }
        finally
        {
            participant.ReleaseCommit();
            await IgnoreFailureAsync(reload);
            await IgnoreFailureAsync(waiter?.Worker);
        }
    }

    [Fact(DisplayName = "GetOrActivate returns the restored generation after reload commit failure")]
    public async Task GetOrActivate_AfterReloadCommitFailure_ReturnsRestoredGeneration()
    {
        var participant = new GatedActivationParticipant(gatedGeneration: 2, failGeneration: 2);
        await using var host = BuildHost(
            cshells => cshells.WithAssemblies().AddShell("settlement", _ => { }),
            services => services.AddSingleton<IShellGenerationActivationParticipant>(participant));
        var registry = host.Services.GetRequiredService<IShellRegistry>();
        var original = await registry.ActivateAsync("settlement");
        Task<ReloadResult>? reload = null;
        PendingRequest? waiter = null;

        try
        {
            reload = Task.Run(() => registry.ReloadAsync("settlement"));
            var rejectedCandidate = await participant.CommitEntered.WaitAsync(Timeout);
            waiter = await StartGetOrActivateAsync(registry, "settlement");
            Assert.False(waiter.Operation.IsCompleted);

            participant.ReleaseCommit();
            var result = await reload.WaitAsync(Timeout);
            var waiterResult = await waiter.Worker.WaitAsync(Timeout);

            Assert.IsType<ShellGenerationActivationException>(result.Error);
            Assert.Null(result.NewShell);
            Assert.False(((Shell)rejectedCandidate).IsActivationCommitted);
            Assert.Same(original, waiterResult);
            Assert.True(((Shell)waiterResult).IsActivationCommitted);
            Assert.Same(original, registry.GetActive("settlement"));
        }
        finally
        {
            participant.ReleaseCommit();
            await IgnoreFailureAsync(reload);
            await IgnoreFailureAsync(waiter?.Worker);
        }
    }

    [Fact(DisplayName = "Cancelling a waiter does not cancel activation commit")]
    public async Task GetOrActivate_CancelledWaiter_DoesNotCancelActivation()
    {
        var participant = new GatedActivationParticipant();
        await using var host = BuildHost(
            cshells => cshells.WithAssemblies().AddShell("settlement", _ => { }),
            services => services.AddSingleton<IShellGenerationActivationParticipant>(participant));
        var registry = host.Services.GetRequiredService<IShellRegistry>();
        using var cancellation = new CancellationTokenSource();
        Task<IShell>? activation = null;
        PendingRequest? waiter = null;

        try
        {
            activation = Task.Run(() => registry.ActivateAsync("settlement"));
            await participant.CommitEntered.WaitAsync(Timeout);
            waiter = await StartGetOrActivateAsync(registry, "settlement", cancellation.Token);
            Assert.False(waiter.Operation.IsCompleted);

            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await waiter.Worker.WaitAsync(Timeout));
            Assert.False(activation.IsCompleted);

            participant.ReleaseCommit();
            var activated = await activation.WaitAsync(Timeout);
            Assert.True(((Shell)activated).IsActivationCommitted);
        }
        finally
        {
            participant.ReleaseCommit();
            await IgnoreFailureAsync(activation);
            await IgnoreFailureAsync(waiter?.Worker);
        }
    }

    [Fact(DisplayName = "A candidate removed during commit is rejected and never marked committed")]
    public async Task GetOrActivate_CandidateRemovedBeforeCommitCompletion_RunsRecovery()
    {
        var participant = new GatedActivationParticipant(gatedGeneration: 1);
        await using var host = BuildHost(
            cshells => cshells.WithAssemblies().AddShell("settlement", _ => { }),
            services => services.AddSingleton<IShellGenerationActivationParticipant>(participant));
        var registry = host.Services.GetRequiredService<IShellRegistry>();
        Task<IShell>? activation = null;
        PendingRequest? waiter = null;

        try
        {
            activation = Task.Run(() => registry.ActivateAsync("settlement"));
            var rejectedCandidate = await participant.CommitEntered.WaitAsync(Timeout);
            waiter = await StartGetOrActivateAsync(registry, "settlement");
            Assert.False(waiter.Operation.IsCompleted);

            var drain = await registry.DrainAsync(rejectedCandidate);
            await drain.WaitAsync().WaitAsync(Timeout);
            Assert.Equal(ShellLifecycleState.Disposed, rejectedCandidate.State);

            participant.ReleaseCommit();
            await Assert.ThrowsAsync<ShellGenerationActivationException>(async () => await activation.WaitAsync(Timeout));
            var recovered = await waiter.Worker.WaitAsync(Timeout);

            Assert.False(((Shell)rejectedCandidate).IsActivationCommitted);
            Assert.Equal(2, recovered.Descriptor.Generation);
            Assert.True(((Shell)recovered).IsActivationCommitted);
        }
        finally
        {
            participant.ReleaseCommit();
            await IgnoreFailureAsync(activation);
            await IgnoreFailureAsync(waiter?.Worker);
        }
    }

    [Fact(DisplayName = "A candidate removed by completion is rejected before its committed marker is set")]
    public async Task GetOrActivate_CandidateRemovedDuringComplete_DoesNotMarkCommitted()
    {
        var participant = new CompleteRemovalParticipant();
        await using var host = BuildHost(
            cshells => cshells.WithAssemblies().AddShell("settlement", _ => { }),
            services => services.AddSingleton<IShellGenerationActivationParticipant>(participant));
        var registry = host.Services.GetRequiredService<IShellRegistry>();
        participant.Registry = registry;
        Task<IShell>? activation = null;
        PendingRequest? waiter = null;

        try
        {
            activation = Task.Run(() => registry.ActivateAsync("settlement"));
            var removedCandidate = await participant.CommitEntered.WaitAsync(Timeout);
            waiter = await StartGetOrActivateAsync(registry, "settlement");
            Assert.False(waiter.Operation.IsCompleted);

            participant.ReleaseCommit();
            await Assert.ThrowsAsync<ShellGenerationActivationException>(async () => await activation.WaitAsync(Timeout));
            var recovered = await waiter.Worker.WaitAsync(Timeout);

            Assert.Equal(ShellLifecycleState.Disposed, removedCandidate.State);
            Assert.False(((Shell)removedCandidate).IsActivationCommitted);
            Assert.Equal(2, recovered.Descriptor.Generation);
            Assert.True(((Shell)recovered).IsActivationCommitted);
        }
        finally
        {
            participant.ReleaseCommit();
            await IgnoreFailureAsync(activation);
            await IgnoreFailureAsync(waiter?.Worker);
        }
    }

    [Fact(DisplayName = "A failing completion logger does not leave activation unsettled")]
    public async Task GetOrActivate_CompletionAndErrorLoggerFail_MarksCommitted()
    {
        var logger = new ThrowingLogger(LogLevel.Error, "completing an activation participant");
        var laterParticipant = new RecordingCompleteParticipant();
        await using var host = BuildHost(
            cshells => cshells.WithAssemblies().AddShell("settlement", _ => { }),
            services =>
            {
                services.AddSingleton<IShellGenerationActivationParticipant, ThrowingCompleteParticipant>();
                services.AddSingleton<IShellGenerationActivationParticipant>(laterParticipant);
                services.AddSingleton<ILogger<ShellRegistry>>(logger);
            });
        var registry = host.Services.GetRequiredService<IShellRegistry>();

        var activated = await registry.GetOrActivateAsync("settlement");

        Assert.True(((Shell)activated).IsActivationCommitted);
        Assert.Same(activated, await registry.GetOrActivateAsync("settlement"));
        Assert.Equal(1, logger.ThrowCount);
        Assert.Equal(1, laterParticipant.CompleteCount);
    }

    [Fact(DisplayName = "Commit logging failure does not revoke settled activation")]
    public async Task GetOrActivate_SuccessLoggerThrows_KeepsCommittedMarker()
    {
        await using var host = BuildHost(
            cshells => cshells.WithAssemblies().AddShell("settlement", _ => { }),
            services => services.AddSingleton<ILogger<ShellRegistry>>(
                new ThrowingLogger(LogLevel.Information, "Activated shell")));
        var registry = host.Services.GetRequiredService<IShellRegistry>();

        await Assert.ThrowsAsync<LogFailureException>(() => registry.GetOrActivateAsync("settlement"));
        var active = Assert.IsType<Shell>(registry.GetActive("settlement"));

        Assert.True(active.IsActivationCommitted);
        Assert.Same(active, await registry.GetOrActivateAsync("settlement"));
    }

    [Fact(DisplayName = "Concurrent requests without activation participants share one generation")]
    public async Task GetOrActivate_ConcurrentWithoutParticipants_BuildsOneGeneration()
    {
        var blueprint = new GatedComposeBlueprint("settlement", gateOnCompose: 1);
        await using var host = BuildHost(cshells => cshells.WithAssemblies().AddBlueprint(blueprint));
        var registry = host.Services.GetRequiredService<IShellRegistry>();
        Task<IShell>? activation = null;
        var waiters = new List<PendingRequest>();

        try
        {
            activation = Task.Run(() => registry.GetOrActivateAsync("settlement"));
            await blueprint.ComposeEntered.WaitAsync(Timeout);
            foreach (var _ in Enumerable.Range(0, 11))
                waiters.Add(await StartGetOrActivateAsync(registry, "settlement"));
            Assert.All(waiters, waiter => Assert.False(waiter.Operation.IsCompleted));

            blueprint.ReleaseCompose();
            var first = await activation.WaitAsync(Timeout);
            var results = await Task.WhenAll(waiters.Select(waiter => waiter.Worker)).WaitAsync(Timeout);

            Assert.All(results, shell => Assert.Same(first, shell));
            Assert.Equal(1, first.Descriptor.Generation);
            Assert.Equal(1, blueprint.ComposeCount);
            Assert.True(((Shell)first).IsActivationCommitted);
        }
        finally
        {
            blueprint.ReleaseCompose();
            await IgnoreFailureAsync(activation);
            foreach (var waiter in waiters)
                await IgnoreFailureAsync(waiter.Worker);
        }
    }

    private static TestHost BuildHost(Action<CShellsBuilder> configure, Action<IServiceCollection>? configureServices = null)
        => new(ShellRegistryActivateTests.BuildHost(configure, configureServices));

    private static async Task<PendingRequest> StartGetOrActivateAsync(
        IShellRegistry registry,
        string name,
        CancellationToken cancellationToken = default)
    {
        var operationReady = new TaskCompletionSource<Task<IShell>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = Task.Run(async () =>
        {
            var operation = registry.GetOrActivateAsync(name, cancellationToken);
            operationReady.TrySetResult(operation);
            return await operation.ConfigureAwait(false);
        });

        var pendingOperation = await operationReady.Task.WaitAsync(Timeout);
        return new PendingRequest(pendingOperation, worker);
    }

    private static async Task IgnoreFailureAsync(Task? task)
    {
        if (task is null)
            return;

        try
        {
            await task.WaitAsync(Timeout);
        }
        catch
        {
            // The test body observes expected operation failures; cleanup only ensures workers exit.
        }
    }

    private sealed record PendingRequest(Task<IShell> Operation, Task<IShell> Worker);

    private sealed class TestHost(ServiceProvider services) : IAsyncDisposable
    {
        public ServiceProvider Services { get; } = services;

        public async ValueTask DisposeAsync()
        {
            var failures = new List<Exception>();
            try
            {
                if (Services.GetService<IShellRegistry>() is { } registry)
                {
                    foreach (var shell in registry.GetActiveShells().ToArray())
                    {
                        try
                        {
                            var drain = await registry.DrainAsync(shell);
                            await drain.WaitAsync().WaitAsync(Timeout);
                        }
                        catch (Exception exception)
                        {
                            failures.Add(exception);
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    await Services.DisposeAsync();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            if (failures.Count > 0)
                throw new AggregateException("The activation settlement test host failed to drain or dispose cleanly.", failures);
        }
    }

    private sealed class GatedActivationParticipant(int? gatedGeneration = null, int? failGeneration = null)
        : IShellGenerationActivationParticipant
    {
        private readonly TaskCompletionSource<IShell> commitEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource commitReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IShell> CommitEntered => commitEntered.Task;

        public Task PrepareAsync(IShell shell, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Commit(IShell shell)
        {
            if (gatedGeneration is null || shell.Descriptor.Generation == gatedGeneration)
            {
                commitEntered.TrySetResult(shell);
                commitReleased.Task.WaitAsync(Timeout).GetAwaiter().GetResult();
            }

            if (shell.Descriptor.Generation == failGeneration)
                throw new InvalidOperationException("Commit failed for the configured generation.");
        }

        public void Complete(IShell shell) { }
        public void Rollback(IShell shell) { }
        public void ReleaseCommit() => commitReleased.TrySetResult();
    }

    private sealed class GatedComposeBlueprint(string name, int gateOnCompose) : IShellBlueprint
    {
        private readonly TaskCompletionSource composeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource composeReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int composeCount;

        public string Name { get; } = name;
        public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
        public Task ComposeEntered => composeEntered.Task;
        public int ComposeCount => Volatile.Read(ref composeCount);

        public async Task<ShellSettings> ComposeAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref composeCount) == gateOnCompose)
            {
                composeEntered.TrySetResult();
                await composeReleased.Task.WaitAsync(Timeout, cancellationToken);
            }

            return new ShellSettings(new ShellId(Name));
        }

        public void ReleaseCompose() => composeReleased.TrySetResult();
    }

    private sealed class ThrowingCompleteParticipant : IShellGenerationActivationParticipant
    {
        public Task PrepareAsync(IShell shell, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Commit(IShell shell) { }
        public void Complete(IShell shell) => throw new InvalidOperationException("Completion cleanup failed.");
        public void Rollback(IShell shell) { }
    }

    private sealed class CompleteRemovalParticipant : IShellGenerationActivationParticipant
    {
        private readonly TaskCompletionSource<IShell> commitEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource commitReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IShellRegistry? Registry { get; set; }
        public Task<IShell> CommitEntered => commitEntered.Task;

        public Task PrepareAsync(IShell shell, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Commit(IShell shell)
        {
            if (shell.Descriptor.Generation != 1)
                return;

            commitEntered.TrySetResult(shell);
            commitReleased.Task.WaitAsync(Timeout).GetAwaiter().GetResult();
        }

        public void Complete(IShell shell)
        {
            if (shell.Descriptor.Generation != 1)
                return;

            var registry = Registry ?? throw new InvalidOperationException("The test registry was not assigned.");
            var drain = registry.DrainAsync(shell).GetAwaiter().GetResult();
            drain.WaitAsync().WaitAsync(Timeout).GetAwaiter().GetResult();
        }

        public void Rollback(IShell shell) { }
        public void ReleaseCommit() => commitReleased.TrySetResult();
    }

    private sealed class RecordingCompleteParticipant : IShellGenerationActivationParticipant
    {
        private int completeCount;

        public int CompleteCount => Volatile.Read(ref completeCount);
        public Task PrepareAsync(IShell shell, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Commit(IShell shell) { }
        public void Complete(IShell shell) => Interlocked.Increment(ref completeCount);
        public void Rollback(IShell shell) { }
    }

    private sealed class ThrowingLogger(LogLevel throwLevel, string messageFragment) : ILogger<ShellRegistry>
    {
        private int throwCount;

        public int ThrowCount => Volatile.Read(ref throwCount);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == throwLevel && state?.ToString()?.Contains(messageFragment, StringComparison.Ordinal) == true)
            {
                Interlocked.Increment(ref throwCount);
                throw new LogFailureException("Configured logger failure.");
            }
        }
    }

    private sealed class LogFailureException(string message) : Exception(message);
}
