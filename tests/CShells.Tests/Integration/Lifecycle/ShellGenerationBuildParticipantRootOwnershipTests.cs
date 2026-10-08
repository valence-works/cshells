using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.Lifecycle;

public sealed class ShellGenerationBuildParticipantRootOwnershipTests
{
    [Fact]
    public async Task ParticipantImplementationTypeAndInterfaceAliasStayRootOnlyAcrossActivations()
    {
        await using var host = BuildHost(services =>
        {
            services.AddSingleton<BuildParticipantBase, RootOnlyBuildParticipant>();
            services.AddSingleton<IShellGenerationBuildParticipant>(sp =>
                sp.GetRequiredService<BuildParticipantBase>() as IShellGenerationBuildParticipant
                ?? throw new InvalidOperationException("The root participant registration has an unexpected implementation."));
        });
        var participant = Assert.IsType<RootOnlyBuildParticipant>(host.GetRequiredService<BuildParticipantBase>());
        Assert.Same(participant, host.GetRequiredService<IShellGenerationBuildParticipant>());

        await ActivateDrainAndAssertRootOnlyAsync(host, "first", participant);
        await ActivateDrainAndAssertRootOnlyAsync(host, "second", participant);

        Assert.Equal(2, participant.BeginCount);
        Assert.Equal(0, participant.DisposeCount);
    }

    [Fact]
    public async Task ConcreteFactoryReturningRootParticipantIsNotCopiedOrDisposedByShell()
    {
        var participant = new RootOnlyBuildParticipant();
        var host = BuildHost(services =>
        {
            services.AddSingleton<RootOnlyBuildParticipant>(_ => participant);
            services.AddSingleton<IShellGenerationBuildParticipant>(participant);
        });
        await using (host)
        {
            var rootParticipant = host.GetRequiredService<RootOnlyBuildParticipant>();
            Assert.Same(participant, rootParticipant);

            await ActivateDrainAndAssertRootOnlyAsync(host, "first", participant);
            await ActivateDrainAndAssertRootOnlyAsync(host, "second", participant);

            Assert.Equal(2, participant.BeginCount);
            Assert.Equal(0, participant.DisposeCount);
        }

        Assert.Equal(1, participant.DisposeCount);
    }

    [Fact]
    public async Task ParticipantImplementationInstanceRegisteredUnderBaseTypeStaysRootOnly()
    {
        var participant = new RootOnlyBuildParticipant();
        await using var host = BuildHost(services =>
        {
            services.AddSingleton<BuildParticipantBase>(participant);
            services.AddSingleton<IShellGenerationBuildParticipant>(participant);
        });

        await ActivateDrainAndAssertRootOnlyAsync(host, "first", participant);
        await ActivateDrainAndAssertRootOnlyAsync(host, "second", participant);

        Assert.Equal(2, participant.BeginCount);
        Assert.Equal(0, participant.DisposeCount);
    }

    private static async Task ActivateDrainAndAssertRootOnlyAsync(
        ServiceProvider host,
        string shellName,
        RootOnlyBuildParticipant participant)
    {
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync(shellName);

        try
        {
            Assert.Null(shell.ServiceProvider.GetService<IShellGenerationBuildParticipant>());
            Assert.Null(shell.ServiceProvider.GetService<RootOnlyBuildParticipant>());
            Assert.Null(shell.ServiceProvider.GetService<BuildParticipantBase>());
        }
        finally
        {
            var drain = await registry.DrainAsync(shell);
            await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(0, participant.DisposeCount);
    }

    private static ServiceProvider BuildHost(Action<IServiceCollection> configureRootServices) =>
        ShellRegistryActivateTests.BuildHost(cshells => cshells
                .WithAssemblyContaining<RootOnlyParticipantFeature>()
                .AddShell("first", shell => shell.WithFeature<RootOnlyParticipantFeature>())
                .AddShell("second", shell => shell.WithFeature<RootOnlyParticipantFeature>()),
            configureRootServices);

    public class BuildParticipantBase
    {
    }

    public sealed class RootOnlyBuildParticipant : BuildParticipantBase, IShellGenerationBuildParticipant, IDisposable
    {
        private int beginCount;
        private int disposeCount;

        public int BeginCount => Volatile.Read(ref beginCount);

        public int DisposeCount => Volatile.Read(ref disposeCount);

        public ValueTask<IShellGenerationBuildLease> BeginAsync(
            ShellGenerationBuildContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref beginCount);
            return ValueTask.FromResult<IShellGenerationBuildLease>(NoopLease.Instance);
        }

        public void Dispose() => Interlocked.Increment(ref disposeCount);
    }

    private sealed class NoopLease : IShellGenerationBuildLease
    {
        public static NoopLease Instance { get; } = new();

        public ValueTask OnSnapshotSelectedAsync(
            RuntimeFeatureCatalogSnapshot snapshot,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [ShellFeature("RootOnlyParticipantProbe")]
    public sealed class RootOnlyParticipantFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
        }
    }
}
