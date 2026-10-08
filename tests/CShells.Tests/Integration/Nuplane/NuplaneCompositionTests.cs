using System.Reflection;
using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using CShells.Nuplane;
using Microsoft.Extensions.DependencyInjection;
using Nuplane.Abstractions;
using Nuplane.Loading;

namespace CShells.Tests.Integration.Nuplane;

public sealed class NuplaneCompositionTests
{
    [Fact]
    public async Task AdapterIsOptInAndConstructsAFeatureFromTheLoadedPackageAssembly()
    {
        var packageCatalog = new FakePackageAssemblyCatalog
        {
            Packages = [FakePackageAssemblyCatalog.CreatePackage("features", typeof(DiscoveredPackageFeature).Assembly)]
        };

        await using (var host = NuplaneTestHost.Build(
            services => services.AddSingleton<IPackageAssemblyCatalog>(packageCatalog),
            shells => shells.WithAssemblyContaining<DiscoveredPackageFeature>()
                .AddShell("baseline", shell => shell.WithFeature("NuplaneDiscovered"))))
        {
            await host.GetRequiredService<IRuntimeFeatureCatalog>().GetSnapshotAsync();
            Assert.Equal(0, packageCatalog.QueryCount);
        }

        await using var optedIn = NuplaneTestHost.Build(
            services => services.AddSingleton<IPackageAssemblyCatalog>(packageCatalog),
            shells => shells.WithNuplaneFeatureDiscovery()
                .AddShell("loaded-package", shell => shell.WithFeature("NuplaneDiscovered")));
        var shell = await optedIn.GetRequiredService<IShellRegistry>().ActivateAsync("loaded-package");

        Assert.Equal(1, packageCatalog.QueryCount);
        Assert.NotNull(shell.ServiceProvider.GetService<DiscoveredFeatureMarker>());
        Assert.Contains(
            "NuplaneDiscovered",
            (await optedIn.GetRequiredService<IRuntimeFeatureCatalog>().GetSnapshotAsync()).FeatureDescriptors.Select(feature => feature.Id));
    }

    [Fact]
    public async Task ExplicitNuplaneProviderComposesWithAnExistingAssemblyProvider()
    {
        var packageCatalog = new FakePackageAssemblyCatalog
        {
            Packages = [FakePackageAssemblyCatalog.CreatePackage("features", typeof(DiscoveredPackageFeature).Assembly)]
        };
        var existingProvider = new RecordingAssemblyProvider(typeof(DiscoveredPackageFeature).Assembly);

        await using var host = NuplaneTestHost.Build(
            services => services.AddSingleton<IPackageAssemblyCatalog>(packageCatalog),
            shells => shells.WithAssemblyProvider(existingProvider)
                .WithNuplaneFeatureDiscovery()
                .AddShell("combined", shell => shell.WithFeature("NuplaneDiscovered")));

        var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync("combined");

        Assert.NotNull(shell.ServiceProvider.GetService<DiscoveredFeatureMarker>());
        Assert.Equal(1, existingProvider.QueryCount);
        Assert.Equal(1, packageCatalog.QueryCount);
    }

    [Fact]
    public async Task DisabledObserverLeavesInitialProviderDiscoveryAvailable()
    {
        var packageCatalog = new FakePackageAssemblyCatalog
        {
            Packages = [FakePackageAssemblyCatalog.CreatePackage("features", typeof(DiscoveredPackageFeature).Assembly)]
        };
        await using var host = NuplaneTestHost.Build(
            services => services.AddSingleton<IPackageAssemblyCatalog>(packageCatalog),
            shells => shells.WithNuplaneFeatureDiscovery(options => options.Enabled = false)
                .AddShell("disabled-observer", shell => shell.WithFeature("NuplaneDiscovered")));
        var registry = host.GetRequiredService<IShellRegistry>();
        var shell = await registry.ActivateAsync("disabled-observer");

        await NuplaneCoordinatorTestCases.NotifyAsync(host.GetRequiredService<INuplaneObserver>());

        Assert.Equal(1, packageCatalog.QueryCount);
        Assert.Same(shell, registry.GetActive("disabled-observer"));
        Assert.NotNull(shell.ServiceProvider.GetService<DiscoveredFeatureMarker>());
    }

    [Fact]
    public async Task FailedPromotionAfterColdRefreshReusesTheCommittedCatalogOnTheNextBuild()
    {
        FailOnceInitializer.Reset();
        var packageCatalog = new FakePackageAssemblyCatalog
        {
            Packages = [FakePackageAssemblyCatalog.CreatePackage("features", typeof(FailingPromotionFeature).Assembly)]
        };
        await using var host = NuplaneTestHost.Build(
            services => services.AddSingleton<IPackageAssemblyCatalog>(packageCatalog),
            shells => shells.WithNuplaneFeatureDiscovery()
                .AddShell("fail-once", shell => shell.WithFeature("NuplaneFailPromotion")));
        var registry = host.GetRequiredService<IShellRegistry>();
        await NuplaneCoordinatorTestCases.NotifyAsync(host.GetRequiredService<INuplaneObserver>());

        var firstPromotionFailure = await Record.ExceptionAsync(() => registry.ActivateAsync("fail-once"));

        Assert.NotNull(firstPromotionFailure);
        Assert.Equal(1, packageCatalog.QueryCount);
        var shell = await registry.ActivateAsync("fail-once");

        Assert.NotNull(shell);
        Assert.Equal(1, packageCatalog.QueryCount);
    }

    [Fact(DisplayName = "Dependency-aware options configuration reaches the registered observer")]
    public async Task DependencyAwareConfiguration_RegisteredWithTheObserver_InvokesConfiguredCallback()
    {
        var callbackCount = 0;
        var dependencyCallback = (IReadOnlyList<ReloadResult> _, CancellationToken _) =>
        {
            Interlocked.Increment(ref callbackCount);
            return ValueTask.CompletedTask;
        };
        await using var host = NuplaneTestHost.Build(
            services =>
            {
                services.AddSingleton(new CallbackDependency(dependencyCallback));
                services.AddOptions<NuplaneIntegrationOptions>()
                    .Configure<CallbackDependency>((options, dependency) =>
                    {
                        options.AutoReload = true;
                        options.OnReloadResults = dependency.Callback;
                    });
                services.AddSingleton<IPackageAssemblyCatalog>(new FakePackageAssemblyCatalog());
            },
            shells => shells.WithNuplaneFeatureDiscovery().AddShell("dependency-options", _ => { }));

        var configured = host.GetRequiredService<Microsoft.Extensions.Options.IOptions<NuplaneIntegrationOptions>>().Value;
        Assert.Same(dependencyCallback, configured.OnReloadResults);
        await host.GetRequiredService<IShellRegistry>().ActivateAsync("dependency-options");
        await NuplaneCoordinatorTestCases.NotifyAsync(host.GetRequiredService<INuplaneObserver>());

        Assert.Equal(1, callbackCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootAndShellResolveTheSameCoordinatorRegardlessOfFirstAlias(bool participantFirst)
    {
        var packageCatalog = new FakePackageAssemblyCatalog
        {
            Packages = [FakePackageAssemblyCatalog.CreatePackage("features", typeof(DiscoveredPackageFeature).Assembly)]
        };
        await using var host = NuplaneTestHost.Build(
            services => services.AddSingleton<IPackageAssemblyCatalog>(packageCatalog),
            shells => shells.WithNuplaneFeatureDiscovery()
                .AddShell("aliases", shell => shell.WithFeature("NuplaneDiscovered")));

        IShellGenerationBuildParticipant participant;
        INuplaneObserver observer;
        if (participantFirst)
        {
            participant = host.GetRequiredService<IShellGenerationBuildParticipant>();
            observer = host.GetRequiredService<INuplaneObserver>();
        }
        else
        {
            observer = host.GetRequiredService<INuplaneObserver>();
            participant = host.GetRequiredService<IShellGenerationBuildParticipant>();
        }

        Assert.Same(observer, participant);
        var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync("aliases");
        Assert.Same(observer, shell.ServiceProvider.GetServices<INuplaneObserver>().Last());
        Assert.Null(shell.ServiceProvider.GetService<IShellGenerationBuildParticipant>());
    }

    [Fact]
    public void ResolvingTheCoordinatorDoesNotEagerlyResolveTheShellRegistry()
    {
        var registryResolutions = 0;
        var services = new ServiceCollection();
        services.AddSingleton<IPackageAssemblyCatalog>(new FakePackageAssemblyCatalog());
        services.AddSingleton<IShellRegistry>(_ =>
        {
            registryResolutions++;
            throw new InvalidOperationException("The deferred registry factory ran too early.");
        });
        services.AddCShells(shells => shells.WithNuplaneFeatureDiscovery());
        using var provider = services.BuildServiceProvider();

        var observer = provider.GetRequiredService<INuplaneObserver>();
        var participant = provider.GetRequiredService<IShellGenerationBuildParticipant>();

        Assert.Same(observer, participant);
        Assert.Equal(0, registryResolutions);
    }

    [Fact]
    public async Task AdapterObserverIsAppendedAndUnrelatedObserversKeepTheirOwnShellLifetime()
    {
        var packageCatalog = new FakePackageAssemblyCatalog
        {
            Packages = [FakePackageAssemblyCatalog.CreatePackage("features", typeof(DiscoveredPackageFeature).Assembly)]
        };
        await using var host = NuplaneTestHost.Build(
            services =>
            {
                services.AddSingleton<IPackageAssemblyCatalog>(packageCatalog);
                services.AddSingleton<INuplaneObserver, TrackingObserver>();
            },
            shells => shells.WithNuplaneFeatureDiscovery()
                .AddShell("observer-isolation", shell => shell.WithFeature("NuplaneDiscovered")));

        var rootObservers = host.GetRequiredService<IEnumerable<INuplaneObserver>>().ToArray();
        var rootTrackingObserver = Assert.IsType<TrackingObserver>(rootObservers[0]);
        Assert.Same(rootObservers[1], host.GetRequiredService<IShellGenerationBuildParticipant>());

        var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync("observer-isolation");
        var shellObservers = shell.ServiceProvider.GetServices<INuplaneObserver>().ToArray();
        Assert.Equal(2, shellObservers.Length);
        Assert.NotSame(rootObservers[0], shellObservers[0]);
        var shellTrackingObserver = Assert.IsType<TrackingObserver>(shellObservers[0]);
        Assert.Same(rootObservers[1], shellObservers[1]);

        var drain = await host.GetRequiredService<IShellRegistry>().DrainAsync(shell);
        await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, shellTrackingObserver.DisposalCount);
        Assert.Equal(0, rootTrackingObserver.DisposalCount);

        await host.DisposeAsync();

        Assert.Equal(1, shellTrackingObserver.DisposalCount);
        Assert.Equal(1, rootTrackingObserver.DisposalCount);
    }

    [ShellFeature("NuplaneDiscovered")]
    public sealed class DiscoveredPackageFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) => services.AddSingleton<DiscoveredFeatureMarker>();
    }

    public sealed class DiscoveredFeatureMarker
    {
    }

    [ShellFeature("NuplaneFailPromotion")]
    public sealed class FailingPromotionFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) => services.AddTransient<IShellInitializer, FailOnceInitializer>();
    }

    public sealed class FailOnceInitializer : IShellInitializer
    {
        private static int _attempts;

        public static void Reset() => _attempts = 0;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _attempts) == 1)
                throw new InvalidOperationException("The first promotion is expected to fail.");

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAssemblyProvider(Assembly assembly) : IFeatureAssemblyProvider
    {
        public int QueryCount { get; private set; }

        public Task<IEnumerable<Assembly>> GetAssembliesAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            QueryCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IEnumerable<Assembly>>([assembly]);
        }
    }

    private sealed class TrackingObserver : INuplaneObserver, IDisposable
    {
        private int _disposalCount;

        public int DisposalCount => Volatile.Read(ref _disposalCount);

        public Task OnPackagesChangingAsync(PackageChangeSet changeSet, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OnPackagesChangedAsync(PackageChangeSet changeSet, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OnPackageFailedAsync(string packageId, Exception exception, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OnPackagesReconciledAsync(PackageChangeSet changeSet, IReadOnlyList<ResolvedPackage> appliedPackages, CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose() => Interlocked.Increment(ref _disposalCount);
    }

    private sealed record CallbackDependency(Func<IReadOnlyList<ReloadResult>, CancellationToken, ValueTask> Callback);
}
