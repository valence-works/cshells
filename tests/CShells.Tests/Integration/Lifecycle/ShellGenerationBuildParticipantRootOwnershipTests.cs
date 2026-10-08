using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.Lifecycle;

public sealed class ShellGenerationBuildParticipantRootOwnershipTests
{
    [Fact]
    public async Task ParticipantImplementationTypeAndInterfaceAliasStayRootOnlyAcrossActivations()
    {
        await using var host = BuildHost(services =>
        {
            services.AddSingleton<RootOnlyBuildParticipant>();
            services.AddSingleton<IShellGenerationBuildParticipant>(sp => sp.GetRequiredService<RootOnlyBuildParticipant>());
        });
        var participant = host.GetRequiredService<RootOnlyBuildParticipant>();
        Assert.Same(participant, host.GetRequiredService<IShellGenerationBuildParticipant>());

        await ActivateDrainAndAssertRootOnlyAsync(host, "implementation-first", participant);
        await ActivateDrainAndAssertRootOnlyAsync(host, "implementation-second", participant);

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
        var rootParticipant = host.GetRequiredService<RootOnlyBuildParticipant>();
        Assert.Same(participant, rootParticipant);

        await ActivateDrainAndAssertRootOnlyAsync(host, "factory-first", participant);
        await ActivateDrainAndAssertRootOnlyAsync(host, "factory-second", participant);

        Assert.Equal(2, participant.BeginCount);
        Assert.Equal(0, participant.DisposeCount);

        await host.DisposeAsync();
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

        await ActivateDrainAndAssertRootOnlyAsync(host, "instance-first", participant);
        await ActivateDrainAndAssertRootOnlyAsync(host, "instance-second", participant);

        Assert.Equal(2, participant.BeginCount);
        Assert.Equal(0, participant.DisposeCount);
    }

    private static async Task ActivateDrainAndAssertRootOnlyAsync(
        ShellTestHost host,
        string shellName,
        RootOnlyBuildParticipant participant)
    {
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync(shellName);

        Assert.Null(shell.ServiceProvider.GetService<IShellGenerationBuildParticipant>());
        Assert.Null(shell.ServiceProvider.GetService<RootOnlyBuildParticipant>());
        Assert.Null(shell.ServiceProvider.GetService<BuildParticipantBase>());

        var drain = await registry.DrainAsync(shell);
        await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, participant.DisposeCount);
    }

    private static ShellTestHost BuildHost(Action<IServiceCollection> configureRootServices)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        configureRootServices(services);
        services.AddCShells(builder =>
        {
            builder.WithAssemblyContaining<RootOnlyParticipantFeature>()
                .AddShell("implementation-first", shell => shell.WithFeature<RootOnlyParticipantFeature>())
                .AddShell("implementation-second", shell => shell.WithFeature<RootOnlyParticipantFeature>())
                .AddShell("factory-first", shell => shell.WithFeature<RootOnlyParticipantFeature>())
                .AddShell("factory-second", shell => shell.WithFeature<RootOnlyParticipantFeature>())
                .AddShell("instance-first", shell => shell.WithFeature<RootOnlyParticipantFeature>())
                .AddShell("instance-second", shell => shell.WithFeature<RootOnlyParticipantFeature>());
        });
        return new ShellTestHost(services.BuildServiceProvider());
    }

    private sealed class ShellTestHost(ServiceProvider provider) : IServiceProvider, IAsyncDisposable
    {
        public object? GetService(Type serviceType) => provider.GetService(serviceType);

        public T GetRequiredService<T>() where T : notnull => provider.GetRequiredService<T>();

        public async ValueTask DisposeAsync()
        {
            if (provider.GetService(typeof(IShellRegistry)) is IShellRegistry registry)
            {
                foreach (var shell in registry.GetActiveShells())
                {
                    try
                    {
                        var drain = await registry.DrainAsync(shell);
                        await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch
                    {
                        // Preserve the assertion while continuing teardown of remaining shells.
                    }
                }
            }

            await provider.DisposeAsync();
        }
    }

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
