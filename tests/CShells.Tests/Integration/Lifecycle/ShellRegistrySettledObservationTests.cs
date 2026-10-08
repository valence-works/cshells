using CShells.DependencyInjection;
using CShells.Lifecycle;
using CShells.Lifecycle.Blueprints;
using CShells.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.Lifecycle;

public sealed class ShellRegistrySettledObservationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact(DisplayName = "Settled observation is nonactivating, validates names, and matches case insensitively")]
    public async Task GetSettledActive_ColdUnknownAndInvalidName_DoesNoWork()
    {
        var blueprints = new StubShellBlueprintProvider().Add("orders");
        await using var host = BuildHostWith(blueprints, "orders");
        var registry = host.Registry;
        var settled = Assert.IsAssignableFrom<ISettledShellRegistry>(registry);

        Assert.Null(settled.GetSettledActive("orders"));
        Assert.Null(settled.GetSettledActive("unknown"));
        Assert.Equal(0, blueprints.LookupCount);
        Assert.Throws<ArgumentException>(() => settled.GetSettledActive(" "));
        Assert.DoesNotContain(typeof(ISettledShellRegistry), typeof(IShellRegistry).GetInterfaces());

        var active = await registry.ActivateAsync("orders");

        Assert.Equal(1, blueprints.LookupCount);
        Assert.Same(active, settled.GetSettledActive("ORDERS"));
        Assert.Null(host.Services.GetService<ISettledShellRegistry>());
    }

    [Fact(DisplayName = "Settled observation excludes initial candidates through commit and completion")]
    public async Task GetSettledActive_InitialActivationPending_ReturnsOnlyAfterSettlement()
    {
        var participant = new GatedParticipant(gatedGeneration: 1, gateComplete: true);
        await using var host = BuildHost("orders",
            cshells => cshells.WithAssemblies().AddShell("orders", _ => { }),
            services => services.AddSingleton<IShellGenerationActivationParticipant>(participant));
        var settled = Assert.IsAssignableFrom<ISettledShellRegistry>(host.Registry);
        Task<IShell>? activation = null;

        try
        {
            activation = Task.Run(() => host.Registry.ActivateAsync("orders"));
            var candidate = await participant.CommitEntered.WaitAsync(Timeout);
            Assert.Same(candidate, host.Registry.GetActive("orders"));
            Assert.Null(settled.GetSettledActive("orders"));

            participant.ReleaseCommit();
            await participant.CompleteEntered.WaitAsync(Timeout);
            Assert.Null(settled.GetSettledActive("orders"));

            participant.ReleaseComplete();
            Assert.Same(candidate, await activation.WaitAsync(Timeout));
            Assert.Same(candidate, settled.GetSettledActive("orders"));
        }
        finally
        {
            participant.ReleaseCommit();
            participant.ReleaseComplete();
            await JoinAsync(activation);
        }
    }

    [Fact(DisplayName = "Rejected initial activation is never observed as settled")]
    public async Task GetSettledActive_InitialCommitRejected_ReturnsNullAfterRollback()
    {
        var participant = new GatedParticipant(gatedGeneration: 1, failCommit: true);
        await using var host = BuildHost("orders",
            cshells => cshells.WithAssemblies().AddShell("orders", _ => { }),
            services => services.AddSingleton<IShellGenerationActivationParticipant>(participant));
        var settled = Assert.IsAssignableFrom<ISettledShellRegistry>(host.Registry);
        Task<IShell>? activation = null;

        try
        {
            activation = Task.Run(() => host.Registry.ActivateAsync("orders"));
            await participant.CommitEntered.WaitAsync(Timeout);
            Assert.Null(settled.GetSettledActive("orders"));
            participant.ReleaseCommit();

            await Assert.ThrowsAsync<ShellGenerationActivationException>(() => activation.WaitAsync(Timeout));
            Assert.Null(host.Registry.GetActive("orders"));
            Assert.Null(settled.GetSettledActive("orders"));
        }
        finally
        {
            participant.ReleaseCommit();
            await JoinAsync(activation, expectActivationFailure: true);
        }
    }

    [Fact(DisplayName = "Completion callback failure remains diagnostic and does not block settled observation")]
    public async Task GetSettledActive_CompleteCallbackThrows_ReturnsCommittedGeneration()
    {
        await using var host = BuildHost("orders",
            cshells => cshells.WithAssemblies().AddShell("orders", _ => { }),
            services => services.AddSingleton<IShellGenerationActivationParticipant, ThrowingCompleteParticipant>());

        var active = await host.Registry.ActivateAsync("orders");

        Assert.Same(active, Assert.IsAssignableFrom<ISettledShellRegistry>(host.Registry).GetSettledActive("orders"));
    }

    [Fact(DisplayName = "A candidate drained during completion is not reported as settled")]
    public async Task GetSettledActive_CandidateDrainedDuringComplete_ReturnsNull()
    {
        var participant = new GatedParticipant(gatedGeneration: 1, gateComplete: true);
        await using var host = BuildHost("orders",
            cshells => cshells.WithAssemblies().AddShell("orders", _ => { }),
            services => services.AddSingleton<IShellGenerationActivationParticipant>(participant));
        var settled = Assert.IsAssignableFrom<ISettledShellRegistry>(host.Registry);
        Task<IShell>? activation = null;
        Task? drainTask = null;

        try
        {
            activation = Task.Run(() => host.Registry.ActivateAsync("orders"));
            var candidate = await participant.CommitEntered.WaitAsync(Timeout);
            participant.ReleaseCommit();
            await participant.CompleteEntered.WaitAsync(Timeout);

            var drain = await host.Registry.DrainAsync(candidate);
            drainTask = drain.WaitAsync();
            await drainTask.WaitAsync(Timeout);
            Assert.Null(settled.GetSettledActive("orders"));

            participant.ReleaseComplete();
            await Assert.ThrowsAsync<ShellGenerationActivationException>(() => activation.WaitAsync(Timeout));
            Assert.Null(settled.GetSettledActive("orders"));
        }
        finally
        {
            participant.ReleaseCommit();
            participant.ReleaseComplete();
            try
            {
                await JoinAsync(drainTask);
            }
            finally
            {
                await JoinAsync(activation, expectActivationFailure: true);
            }
        }
    }

    [Fact(DisplayName = "Settled observation follows the current generation through reload composition and settlement")]
    public async Task GetSettledActive_ReloadPendingAndSuccessful_TracksCurrentGeneration()
    {
        var blueprint = new GatedBlueprint("orders", gateOnCompose: 2);
        var participant = new GatedParticipant(gatedGeneration: 2, gateComplete: true);
        await using var host = BuildHost("orders",
            cshells => cshells.WithAssemblies().AddBlueprint(blueprint),
            services => services.AddSingleton<IShellGenerationActivationParticipant>(participant));
        var settled = Assert.IsAssignableFrom<ISettledShellRegistry>(host.Registry);
        var original = await host.Registry.ActivateAsync("orders");
        Task<ReloadResult>? reload = null;

        try
        {
            reload = Task.Run(() => host.Registry.ReloadAsync("orders"));
            await blueprint.ComposeEntered.WaitAsync(Timeout);
            Assert.Same(original, settled.GetSettledActive("orders"));

            blueprint.ReleaseCompose();
            var candidate = await participant.CommitEntered.WaitAsync(Timeout);
            Assert.Same(candidate, host.Registry.GetActive("orders"));
            Assert.Null(settled.GetSettledActive("orders"));

            participant.ReleaseCommit();
            await participant.CompleteEntered.WaitAsync(Timeout);
            Assert.Null(settled.GetSettledActive("orders"));
            participant.ReleaseComplete();

            var result = await reload.WaitAsync(Timeout);
            Assert.Null(result.Error);
            Assert.Same(candidate, settled.GetSettledActive("orders"));
            await result.Drain!.WaitAsync().WaitAsync(Timeout);
            Assert.Same(candidate, settled.GetSettledActive("orders"));
        }
        finally
        {
            blueprint.ReleaseCompose();
            participant.ReleaseCommit();
            participant.ReleaseComplete();
            await JoinAsync(reload);
        }
    }

    [Fact(DisplayName = "Rejected reload restores only the prior settled generation")]
    public async Task GetSettledActive_ReloadCommitRejected_RestoresOriginal()
    {
        var participant = new GatedParticipant(gatedGeneration: 2, failCommit: true);
        await using var host = BuildHost("orders",
            cshells => cshells.WithAssemblies().AddShell("orders", _ => { }),
            services => services.AddSingleton<IShellGenerationActivationParticipant>(participant));
        var settled = Assert.IsAssignableFrom<ISettledShellRegistry>(host.Registry);
        var original = await host.Registry.ActivateAsync("orders");
        Task<ReloadResult>? reload = null;

        try
        {
            reload = Task.Run(() => host.Registry.ReloadAsync("orders"));
            await participant.CommitEntered.WaitAsync(Timeout);
            Assert.Null(settled.GetSettledActive("orders"));
            participant.ReleaseCommit();

            var result = await reload.WaitAsync(Timeout);
            Assert.IsType<ShellGenerationActivationException>(result.Error);
            Assert.Same(original, host.Registry.GetActive("orders"));
            Assert.Same(original, settled.GetSettledActive("orders"));
        }
        finally
        {
            participant.ReleaseCommit();
            await JoinAsync(reload);
        }
    }

    [Fact(DisplayName = "Settled observation excludes drained and unregistered generations")]
    public async Task GetSettledActive_DrainAndUnregister_ReturnsNull()
    {
        var manager = new StubShellBlueprintManager();
        var blueprints = new StubShellBlueprintProvider().Add("drain", manager: manager).Add("remove", manager: manager);
        await using var host = BuildHostWith(blueprints, "drain", "remove");
        var settled = Assert.IsAssignableFrom<ISettledShellRegistry>(host.Registry);

        var drained = await host.Registry.ActivateAsync("drain");
        Assert.Same(drained, settled.GetSettledActive("drain"));
        var drain = await host.Registry.DrainAsync(drained);
        await drain.WaitAsync().WaitAsync(Timeout);
        Assert.Null(settled.GetSettledActive("drain"));

        var removed = await host.Registry.ActivateAsync("remove");
        await host.Registry.UnregisterBlueprintAsync("remove");
        Assert.Equal(ShellLifecycleState.Disposed, removed.State);
        Assert.Null(settled.GetSettledActive("remove"));
    }

    private static TestHost BuildHost(string shellName, Action<CShellsBuilder> configure, Action<IServiceCollection>? configureServices = null) =>
        new(ShellRegistryActivateTests.BuildHost(configure, configureServices), [shellName]);

    private static TestHost BuildHostWith(StubShellBlueprintProvider blueprints, params string[] shellNames)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShells(cshells =>
        {
            cshells.WithAssemblies();
            cshells.AddBlueprintProvider(_ => blueprints);
        });
        return new TestHost(services.BuildServiceProvider(), shellNames);
    }

    private static async Task JoinAsync(Task? operation, bool expectActivationFailure = false)
    {
        if (operation is null)
            return;

        try
        {
            await operation.WaitAsync(Timeout);
        }
        catch (ShellGenerationActivationException) when (expectActivationFailure)
        {
            // The expected rejected activation is asserted by the test body.
        }
    }

    private sealed class TestHost(ServiceProvider services, IReadOnlyList<string> shellNames) : IAsyncDisposable
    {
        public ServiceProvider Services { get; } = services;
        private IReadOnlyList<string> ShellNames { get; } = shellNames;
        public IShellRegistry Registry => Services.GetRequiredService<IShellRegistry>();

        public async ValueTask DisposeAsync()
        {
            var failures = new List<Exception>();
            if (Services.GetService<IShellRegistry>() is { } registry)
            {
                var shells = ShellNames.SelectMany(registry.GetAll).Distinct().ToArray();
                foreach (var shell in shells)
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

            try
            {
                await Services.DisposeAsync().AsTask().WaitAsync(Timeout);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            if (failures.Count > 0)
                throw new AggregateException("The settled observation host failed to drain or dispose cleanly.", failures);
        }
    }

    private sealed class GatedBlueprint(string name, int gateOnCompose) : IShellBlueprint
    {
        private readonly TaskCompletionSource composeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource composeReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int composeCount;

        public string Name { get; } = name;
        public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
        public Task ComposeEntered => composeEntered.Task;

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

    private sealed class GatedParticipant(int gatedGeneration, bool gateComplete = false, bool failCommit = false)
        : IShellGenerationActivationParticipant
    {
        private readonly TaskCompletionSource<IShell> commitEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource commitReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource completeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource completeReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IShell> CommitEntered => commitEntered.Task;
        public Task CompleteEntered => completeEntered.Task;

        public Task PrepareAsync(IShell shell, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Commit(IShell shell)
        {
            if (shell.Descriptor.Generation != gatedGeneration)
                return;

            commitEntered.TrySetResult(shell);
            commitReleased.Task.WaitAsync(Timeout).GetAwaiter().GetResult();
            if (failCommit)
                throw new InvalidOperationException("The configured activation commit failed.");
        }

        public void Complete(IShell shell)
        {
            if (shell.Descriptor.Generation != gatedGeneration)
                return;

            completeEntered.TrySetResult();
            if (gateComplete)
                completeReleased.Task.WaitAsync(Timeout).GetAwaiter().GetResult();
        }

        public void Rollback(IShell shell) { }
        public void ReleaseCommit() => commitReleased.TrySetResult();
        public void ReleaseComplete() => completeReleased.TrySetResult();
    }

    private sealed class ThrowingCompleteParticipant : IShellGenerationActivationParticipant
    {
        public Task PrepareAsync(IShell shell, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Commit(IShell shell) { }
        public void Complete(IShell shell) => throw new InvalidOperationException("Completion is diagnostic only.");
        public void Rollback(IShell shell) { }
    }
}
