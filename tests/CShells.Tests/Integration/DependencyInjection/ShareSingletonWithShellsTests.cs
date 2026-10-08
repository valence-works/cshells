using CShells.Configuration;
using CShells.DependencyInjection;
using CShells.Features;
using CShells.Hosting;
using CShells.Lifecycle;
using CShells.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CShells.Tests.Integration.DependencyInjection;

public sealed class ShareSingletonWithShellsTests
{
    [Fact(DisplayName = "Selected singleton instances are shared through overlapping shell generations and disposed by root once")]
    public async Task ShareSingletonWithShells_OverlappingGenerations_RootOwnsDisposal()
    {
        var disposalCounts = new DisposalCounts();
        await using (var host = BuildHost(
            services =>
            {
                services.AddSingleton(disposalCounts);
                services.AddSingleton<TypeOwnedProbe>();
                services.AddSingleton<ISharedProbe>(_ => new SharedProbe(disposalCounts.IncrementFactory));
            },
            builder => builder
                .ShareSingletonWithShells<ISharedProbe>()
                .ShareSingletonWithShells<TypeOwnedProbe>()
                .AddShell("tenant", _ => { })))
        {
            var rootInstance = host.GetRequiredService<ISharedProbe>();
            var typeInstance = host.GetRequiredService<TypeOwnedProbe>();
            var registry = host.GetRequiredService<IShellRegistry>();
            var first = await registry.ActivateAsync("tenant");
            await using var activeScope = first.BeginScope();

            var reload = await registry.ReloadAsync("tenant");

            Assert.Null(reload.Error);
            Assert.NotNull(reload.NewShell);
            Assert.NotNull(reload.Drain);
            Assert.Same(rootInstance, first.ServiceProvider.GetRequiredService<ISharedProbe>());
            Assert.Same(rootInstance, reload.NewShell!.ServiceProvider.GetRequiredService<ISharedProbe>());
            Assert.Same(typeInstance, first.ServiceProvider.GetRequiredService<TypeOwnedProbe>());
            Assert.Same(typeInstance, reload.NewShell.ServiceProvider.GetRequiredService<TypeOwnedProbe>());
            Assert.Equal(0, disposalCounts.FactoryDisposals);
            Assert.Equal(0, disposalCounts.TypeDisposals);

            await activeScope.DisposeAsync();
            await reload.Drain!.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(0, disposalCounts.FactoryDisposals);
            Assert.Equal(0, disposalCounts.TypeDisposals);
        }

        Assert.Equal(1, disposalCounts.FactoryDisposals);
        Assert.Equal(1, disposalCounts.TypeDisposals);
    }

    [Fact(DisplayName = "Root disposes a shared async-only singleton once after disposing its shell")]
    public async Task ShareSingletonWithShells_AsyncDisposable_RootOwnsAsyncDisposal()
    {
        var disposeCount = 0;
        await using (var host = BuildHost(
            services => services.AddSingleton<IAsyncProbe>(_ => new AsyncProbe(() => disposeCount++)),
            builder => builder
                .ShareSingletonWithShells<IAsyncProbe>()
                .AddShell("tenant", _ => { })))
        {
            var rootInstance = host.GetRequiredService<IAsyncProbe>();
            var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync("tenant");

            Assert.Same(rootInstance, shell.ServiceProvider.GetRequiredService<IAsyncProbe>());
            var drain = await host.GetRequiredService<IShellRegistry>().DrainAsync(shell);
            await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(0, disposeCount);
        }

        Assert.Equal(1, disposeCount);
    }

    [Fact(DisplayName = "All unkeyed registrations are shared in descriptor order while keyed registrations remain independent")]
    public async Task ShareSingletonWithShells_MultipleRegistrations_PreservesOrderAndKeyBehavior()
    {
        var callerDisposals = 0;
        var rootDisposals = 0;
        var first = new SharedProbe(() => callerDisposals++);
        var second = new SharedProbe(() => rootDisposals++);
        await using (var host = BuildHost(
            services =>
            {
                services.AddSingleton<ISharedProbe>(first);
                services.AddSingleton<ISharedProbe>(_ => second);
                services.AddKeyedSingleton<ISharedProbe>("primary", (_, _) => new SharedProbe());
            },
            builder => builder
                .ShareSingletonWithShells(typeof(ISharedProbe))
                .AddShell("tenant", _ => { })))
        {
            var rootKeyedInstance = host.GetRequiredKeyedService<ISharedProbe>("primary");
            Assert.Same(rootKeyedInstance, host.GetRequiredKeyedService<ISharedProbe>("primary"));
            var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync("tenant");
            var instances = shell.ServiceProvider.GetServices<ISharedProbe>().ToArray();

            Assert.Equal(2, instances.Length);
            Assert.Same(first, instances[0]);
            Assert.Same(second, instances[1]);
            var shellKeyedInstance = shell.ServiceProvider.GetRequiredKeyedService<ISharedProbe>("primary");
            Assert.NotSame(rootKeyedInstance, shellKeyedInstance);
            Assert.Same(shellKeyedInstance, shell.ServiceProvider.GetRequiredKeyedService<ISharedProbe>("primary"));
        }

        Assert.Equal(0, callerDisposals);
        Assert.Equal(1, rootDisposals);
        first.Dispose();
        Assert.Equal(1, callerDisposals);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task ShareSingletonWithShells_ExplicitEnumerableOverride_ThrowsActionableError(
        int enumerableCount,
        bool registerAfterAddCShells)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<ISharedProbe>(_ => new SharedProbe());

        if (!registerAfterAddCShells)
            AddEnumerableOverride(services, enumerableCount);

        services.AddCShells(builder => builder
            .WithAssemblies()
            .ShareSingletonWithShells<ISharedProbe>()
            .AddShell("tenant", _ => { }));

        if (registerAfterAddCShells)
            AddEnumerableOverride(services, enumerableCount);

        await using var host = services.BuildServiceProvider();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.GetRequiredService<IShellRegistry>().ActivateAsync("tenant"));

        Assert.Contains(typeof(IEnumerable<ISharedProbe>).ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("do not select", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShareSingletonWithShells_OpenGenericEnumerableOverride_ThrowsActionableError()
    {
        await using var host = BuildHost(
            services =>
            {
                services.AddSingleton<ISharedProbe>(_ => new SharedProbe());
                services.AddSingleton(typeof(IEnumerable<>), typeof(EmptyEnumerable<>));
            },
            builder => builder.ShareSingletonWithShells<ISharedProbe>().AddShell("tenant", _ => { }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.GetRequiredService<IShellRegistry>().ActivateAsync("tenant"));

        Assert.Contains(typeof(IEnumerable<>).ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShareSingletonWithShells_KeyedEnumerableOverride_IsIndependent()
    {
        var rootInstance = new SharedProbe();
        var keyedInstances = new ISharedProbe[] { new SharedProbe() };
        await using var host = BuildHost(
            services =>
            {
                services.AddSingleton<ISharedProbe>(rootInstance);
                services.AddKeyedSingleton<IEnumerable<ISharedProbe>>("custom", keyedInstances);
            },
            builder => builder.ShareSingletonWithShells<ISharedProbe>().AddShell("tenant", _ => { }));

        var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync("tenant");

        Assert.Same(rootInstance, Assert.Single(shell.ServiceProvider.GetServices<ISharedProbe>()));
        Assert.Same(keyedInstances, shell.ServiceProvider.GetRequiredKeyedService<IEnumerable<ISharedProbe>>("custom"));
    }

    [Fact]
    public async Task ShareSingletonWithShells_UnselectedExplicitEnumerable_RemainsUnchanged()
    {
        var explicitEnumerable = new ISharedProbe[] { new SharedProbe(), new SharedProbe() };
        await using var host = BuildHost(
            services =>
            {
                services.AddSingleton<ISharedProbe>(_ => new SharedProbe());
                services.AddSingleton<IEnumerable<ISharedProbe>>(explicitEnumerable);
            },
            builder => builder.AddShell("tenant", _ => { }));

        var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync("tenant");

        Assert.Same(explicitEnumerable, shell.ServiceProvider.GetRequiredService<IEnumerable<ISharedProbe>>());
        Assert.Equal(2, shell.ServiceProvider.GetServices<ISharedProbe>().Count());
    }

    [Fact]
    public async Task AddCShells_RepeatedCallsReuseBuilderAndRegisterInfrastructureOnce()
    {
        var rootInstance = new SharedProbe();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<ISharedProbe>(rootInstance);

        var firstBuilder = services.AddCShells(builder => builder
            .WithAssemblies()
            .ConfigureAllShells(shell => shell.WithConfiguration("FirstDefault", "present"))
            .AddShell("one", _ => { }));
        var secondBuilder = services.AddCShells(builder => builder
            .ShareSingletonWithShells<ISharedProbe>()
            .ConfigureAllShells(shell => shell.WithConfiguration("SecondDefault", "present"))
            .AddShell("two", _ => { }));

        Assert.Same(firstBuilder, secondBuilder);
        await using var host = services.BuildServiceProvider();
        var registry = host.GetRequiredService<IShellRegistry>();
        var one = await registry.ActivateAsync("one");
        var two = await registry.ActivateAsync("two");

        Assert.Same(rootInstance, one.ServiceProvider.GetRequiredService<ISharedProbe>());
        Assert.Same(rootInstance, two.ServiceProvider.GetRequiredService<ISharedProbe>());
        foreach (var shell in new[] { one, two })
        {
            var settings = shell.ServiceProvider.GetRequiredService<ShellSettings>();
            Assert.Equal("present", settings.GetConfiguration("FirstDefault"));
            Assert.Equal("present", settings.GetConfiguration("SecondDefault"));
        }

        Assert.Single(host.GetServices<IShellLifecycleSubscriber>());
        Assert.Single(host.GetServices<IHostedService>().OfType<CShellsStartupHostedService>());
        Assert.Single(host.GetServices<IShellServiceExclusionProvider>().OfType<DefaultShellServiceExclusionProvider>());
    }

    [Fact]
    public void AddCShells_RepeatedCallsKeepPreExistingBlueprintProviderGuard()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IShellBlueprintProvider>(_ => new StubShellBlueprintProvider());
        services.AddCShells(builder => builder.WithAssemblies());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddCShells(builder => builder.AddShell("ignored", _ => { })));

        Assert.Contains("pre-existing IShellBlueprintProvider", exception.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Unselected singleton registrations keep per-shell copy behavior")]
    public async Task ShareSingletonWithShells_UnselectedType_RemainsCopied()
    {
        await using var host = BuildHost(
            services =>
            {
                services.AddSingleton<ISharedProbe, SharedProbe>();
                services.AddSingleton<UnselectedProbe>();
                services.AddSingleton<SharedProbe>();
            },
            builder => builder
                .ShareSingletonWithShells<ISharedProbe>()
                .AddShell("one", _ => { })
                .AddShell("two", _ => { }));

        var registry = host.GetRequiredService<IShellRegistry>();
        var one = await registry.ActivateAsync("one");
        var two = await registry.ActivateAsync("two");
        var sharedInstance = host.GetRequiredService<ISharedProbe>();

        Assert.Same(sharedInstance, one.ServiceProvider.GetRequiredService<ISharedProbe>());
        Assert.Same(sharedInstance, two.ServiceProvider.GetRequiredService<ISharedProbe>());
        Assert.NotSame(one.ServiceProvider.GetRequiredService<UnselectedProbe>(), two.ServiceProvider.GetRequiredService<UnselectedProbe>());
        Assert.NotSame(host.GetRequiredService<SharedProbe>(), one.ServiceProvider.GetRequiredService<SharedProbe>());
    }

    [Fact(DisplayName = "Later feature registrations retain normal override precedence")]
    public async Task ShareSingletonWithShells_FeatureOverride_RemainsLastRegistration()
    {
        var hostInstance = new SharedProbe();
        await using var host = BuildHost(
            services => services.AddSingleton<ISharedProbe>(hostInstance),
            builder => builder
                .WithAssemblyContaining<SharedProbeOverrideFeature>()
                .ShareSingletonWithShells<ISharedProbe>()
                .AddShell("tenant", shell => shell.WithFeature<SharedProbeOverrideFeature>()));

        var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync("tenant");
        var instances = shell.ServiceProvider.GetServices<ISharedProbe>().ToArray();

        Assert.Equal(2, instances.Length);
        Assert.Same(hostInstance, instances[0]);
        Assert.IsType<SharedProbe>(instances[1]);
        Assert.Same(instances[1], shell.ServiceProvider.GetRequiredService<ISharedProbe>());
    }

    [Fact(DisplayName = "Shared selection validates registrations added after AddCShells")]
    public async Task ShareSingletonWithShells_RegistrationAddedLater_UsesFinalRootSet()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShells(builder => builder
            .WithAssemblies()
            .ShareSingletonWithShells<ISharedProbe>()
            .AddShell("tenant", _ => { }));
        var instance = new SharedProbe();
        services.AddSingleton<ISharedProbe>(instance);
        await using var host = services.BuildServiceProvider();

        var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync("tenant");

        Assert.Same(instance, shell.ServiceProvider.GetRequiredService<ISharedProbe>());
    }

    [Fact(DisplayName = "A selected non-singleton registration fails with lifetime guidance")]
    public async Task ShareSingletonWithShells_ScopedRegistration_ThrowsActionableError()
    {
        await using var host = BuildHost(
            services =>
            {
                services.AddSingleton<ISharedProbe, SharedProbe>();
                services.AddScoped<ISharedProbe, SharedProbe>();
            },
            builder => builder.ShareSingletonWithShells<ISharedProbe>().AddShell("tenant", _ => { }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.GetRequiredService<IShellRegistry>().ActivateAsync("tenant"));

        Assert.Contains(typeof(ISharedProbe).ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("all unkeyed registrations must be singletons", exception.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "A selected missing or open generic service type fails with guidance")]
    public async Task ShareSingletonWithShells_MissingOrOpenType_ThrowsActionableError()
    {
        await using var missingHost = BuildHost(
            _ => { },
            builder => builder.ShareSingletonWithShells<ISharedProbe>().AddShell("tenant", _ => { }));
        await using var openGenericHost = BuildHost(
            services => services.AddSingleton(typeof(IGenericProbe<>), typeof(GenericProbe<>)),
            builder => builder.ShareSingletonWithShells(typeof(IGenericProbe<>)).AddShell("tenant", _ => { }));

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(
            () => missingHost.GetRequiredService<IShellRegistry>().ActivateAsync("tenant"));
        var openGeneric = await Assert.ThrowsAsync<InvalidOperationException>(
            () => openGenericHost.GetRequiredService<IShellRegistry>().ActivateAsync("tenant"));

        Assert.Contains("no unkeyed registration", missing.Message, StringComparison.Ordinal);
        Assert.Contains("open generic", openGeneric.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "An open generic registration cannot silently join a selected closed service set")]
    public async Task ShareSingletonWithShells_ClosedTypeWithOpenRegistration_ThrowsActionableError()
    {
        await using var host = BuildHost(
            services =>
            {
                services.AddSingleton<IGenericProbe<string>, GenericProbe<string>>();
                services.AddSingleton(typeof(IGenericProbe<>), typeof(GenericProbe<>));
            },
            builder => builder.ShareSingletonWithShells<IGenericProbe<string>>().AddShell("tenant", _ => { }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.GetRequiredService<IShellRegistry>().ActivateAsync("tenant"));

        Assert.Contains("open generic registration", exception.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "A selected factory returning null fails with an actionable error")]
    public async Task ShareSingletonWithShells_NullFactoryResult_ThrowsActionableError()
    {
        await using var host = BuildHost(
            services => services.AddSingleton<ISharedProbe>(_ => null!),
            builder => builder.ShareSingletonWithShells<ISharedProbe>().AddShell("tenant", _ => { }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.GetRequiredService<IShellRegistry>().ActivateAsync("tenant"));

        Assert.Contains("factory returned null", exception.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "A selection that conflicts with root-only exclusion fails clearly")]
    public async Task ShareSingletonWithShells_ExcludedType_ThrowsActionableError()
    {
        await using var host = BuildHost(
            services =>
            {
                services.AddSingleton<ISharedProbe, SharedProbe>();
                services.AddSingleton<IShellServiceExclusionProvider>(new ExcludeSharedProbe());
            },
            builder => builder.ShareSingletonWithShells<ISharedProbe>().AddShell("tenant", _ => { }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.GetRequiredService<IShellRegistry>().ActivateAsync("tenant"));

        Assert.Contains("excluded from shell service collections", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildHost(
        Action<IServiceCollection> configureServices,
        Action<CShellsBuilder> configureShells)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        configureServices(services);
        services.AddCShells(builder =>
        {
            builder.WithAssemblies();
            configureShells(builder);
        });

        return services.BuildServiceProvider();
    }

    private sealed class UnselectedProbe;

    private static void AddEnumerableOverride(IServiceCollection services, int count) =>
        services.AddSingleton<IEnumerable<ISharedProbe>>(
            Enumerable.Range(0, count).Select(_ => (ISharedProbe)new SharedProbe()).ToArray());
}

public interface ISharedProbe;

public interface IAsyncProbe;

public sealed class SharedProbe(Action? onDispose = null) : ISharedProbe, IDisposable
{
    public void Dispose() => onDispose?.Invoke();
}

public sealed class AsyncProbe(Action onDispose) : IAsyncProbe, IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        onDispose();
        return ValueTask.CompletedTask;
    }
}

public sealed class TypeOwnedProbe(DisposalCounts counts) : IDisposable
{
    public void Dispose() => counts.IncrementType();
}

public sealed class DisposalCounts
{
    public int FactoryDisposals { get; private set; }

    public int TypeDisposals { get; private set; }

    public void IncrementFactory() => FactoryDisposals++;

    public void IncrementType() => TypeDisposals++;
}

public interface IGenericProbe<T>;

public sealed class GenericProbe<T> : IGenericProbe<T>;

public sealed class EmptyEnumerable<T> : IEnumerable<T>
{
    public IEnumerator<T> GetEnumerator() => Enumerable.Empty<T>().GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

[ShellFeature("SharedProbeOverride")]
public sealed class SharedProbeOverrideFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services) => services.AddSingleton<ISharedProbe, SharedProbe>();
}

public sealed class ExcludeSharedProbe : IShellServiceExclusionProvider
{
    public IEnumerable<Type> GetExcludedServiceTypes() => [typeof(ISharedProbe)];
}
