using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.Lifecycle;

public class ShellSettingsPreparationTests
{
    [Fact(DisplayName = "Settings preparation sees global defaults and dependencies before feature side effects")]
    public async Task Preparation_SeesFinalComposition_AndRunsBeforeFeatureConstruction()
    {
        var probe = new PreparationProbe();
        var preparer = new RecordingPreparer(probe, context =>
        {
            Assert.Equal("payments", context.ShellId.Name);
            Assert.Equal(["PreparationDependency", "PreparationDependent"], context.EnabledFeatureIds);
            Assert.Equal(["PreparationDependent", "MissingFeature"], context.RequestedFeatureIds);
            Assert.Equal(["PreparationDependency"], context.ImplicitFeatureIds);
            Assert.Equal(["MissingFeature"], context.UnknownFeatureIds);
            Assert.False(context.OrderedFeatures.Single(feature => feature.Id == "PreparationDependent").HasConfigurator);
            Assert.Equal("source", context.ConfigurationData["Nested"]);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string?>)context.ConfigurationData)["Nested"] = "mutated");

            var configuration = new Dictionary<string, string?>
            {
                ["PreparationDependent:Value"] = "prepared",
                ["PreparationDependency:Value"] = "dependency-prepared"
            };
            return new ShellSettingsPreparationResult(configuration);
        });

        await using var host = ShellRegistryActivateTests.BuildHost(
            cshells => cshells
                .WithAssemblyContaining<ShellSettingsPreparationTests>()
                .ConfigureAllShells(shell => shell.WithFeature<PreparationDependentFeature>())
                .AddShell("payments", shell => shell
                    .WithConfiguration("Nested", "source")
                    .WithFeatures("MissingFeature")),
            services =>
            {
                services.AddSingleton(probe);
                services.AddSingleton<IShellSettingsPreparer>(preparer);
            });

        var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync("payments");

        Assert.Equal(
            [
                "prepare",
                "ctor:PreparationDependency",
                "services:PreparationDependency:dependency-prepared",
                "ctor:PreparationDependent",
                "configure:prepared",
                "services:PreparationDependent:prepared"
            ],
            probe.Events);
    }

    [Fact(DisplayName = "A preparer can refuse an opaque feature configurator before any feature side effect")]
    public async Task Preparation_RefusesConfigurator_BeforeFeatureSideEffects()
    {
        var probe = new PreparationProbe();
        var preparer = new RecordingPreparer(probe, context =>
        {
            Assert.True(context.OrderedFeatures.Single(feature => feature.Id == "PreparationDependent").HasConfigurator);
            throw new InvalidOperationException("opaque configurator is not supported for this composition");
        });

        await using var host = ShellRegistryActivateTests.BuildHost(
            cshells => cshells
                .WithAssemblyContaining<ShellSettingsPreparationTests>()
                .AddShell("payments", shell => shell.WithFeature<PreparationDependentFeature>(feature => feature.Value = "code")),
            services =>
            {
                services.AddSingleton(probe);
                services.AddSingleton<IShellSettingsPreparer>(preparer);
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.GetRequiredService<IShellRegistry>().ActivateAsync("payments"));

        Assert.Contains("opaque configurator", exception.Message, StringComparison.Ordinal);
        Assert.Equal(["prepare"], probe.Events);
    }

    [Fact(DisplayName = "Multiple settings preparers are rejected before feature construction")]
    public async Task MultiplePreparers_RefuseBeforeFeatureSideEffects()
    {
        var probe = new PreparationProbe();
        await using var host = ShellRegistryActivateTests.BuildHost(
            cshells => cshells
                .WithAssemblyContaining<ShellSettingsPreparationTests>()
                .AddShell("payments", shell => shell.WithFeature<PreparationDependentFeature>()),
            services =>
            {
                services.AddSingleton(probe);
                services.AddSingleton<IShellSettingsPreparer>(new FirstPreparer());
                services.AddSingleton<IShellSettingsPreparer>(new FirstPreparer());
            });

        var exception = Assert.Throws<InvalidOperationException>(
            () => host.GetRequiredService<IShellRegistry>());

        Assert.Contains("at most one IShellSettingsPreparer", exception.Message, StringComparison.Ordinal);
        Assert.Empty(probe.Events);
    }

    [Fact(DisplayName = "Preparation starts from a fresh settings snapshot on every generation")]
    public async Task Preparation_UsesFreshSettingsSnapshot_OnReload()
    {
        var preparedFlags = new List<bool>();
        var preparer = new RecordingPreparer(new PreparationProbe(), context =>
        {
            preparedFlags.Add(context.ConfigurationData.ContainsKey("Prepared"));
            return new ShellSettingsPreparationResult(new Dictionary<string, string?>
            {
                ["Prepared"] = "yes"
            });
        });

        var blueprint = new ReusableSettingsBlueprint("payments");
        await using var host = ShellRegistryActivateTests.BuildHost(
            cshells => cshells.AddBlueprint(blueprint),
            services => services.AddSingleton<IShellSettingsPreparer>(preparer));

        var registry = host.GetRequiredService<IShellRegistry>();
        var first = await registry.ActivateAsync("payments");
        var firstSettings = first.ServiceProvider.GetRequiredService<ShellSettings>();
        var reload = await registry.ReloadAsync("payments");

        Assert.Null(reload.Error);
        Assert.Equal([false, false], preparedFlags);
        Assert.Same(blueprint.TypedValue, firstSettings.ConfigurationData["Typed"]);
        Assert.Same(blueprint.TypedValue, reload.NewShell!.ServiceProvider.GetRequiredService<ShellSettings>().ConfigurationData["Typed"]);
    }

    [Fact(DisplayName = "Cancellation after a preparer returns prevents all feature side effects")]
    public async Task Preparation_IgnoresCancellation_FrameworkStopsBeforeFeatures()
    {
        using var cancellation = new CancellationTokenSource();
        var probe = new PreparationProbe();
        var preparer = new RecordingPreparer(probe, context =>
        {
            cancellation.Cancel();
            return ShellSettingsPreparationResult.Unchanged(context);
        });
        await using var host = BuildPreparedHost(probe, preparer);
        var registry = host.GetRequiredService<IShellRegistry>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            registry.ActivateAsync("payments", cancellationToken: cancellation.Token));

        Assert.Equal(["prepare"], probe.Events);
        Assert.Null(registry.GetActive("payments"));
    }

    [Fact(DisplayName = "Preparation refusal during reload preserves the existing active generation")]
    public async Task Preparation_RefusesReload_PreservesActiveGeneration()
    {
        var refuse = false;
        var probe = new PreparationProbe();
        var preparer = new RecordingPreparer(probe, context => refuse
            ? throw new InvalidOperationException("refused candidate")
            : ShellSettingsPreparationResult.Unchanged(context));
        await using var host = BuildPreparedHost(probe, preparer);
        var registry = host.GetRequiredService<IShellRegistry>();
        var active = await registry.ActivateAsync("payments");
        probe.Events.Clear();
        refuse = true;

        var reload = await registry.ReloadAsync("payments");

        Assert.IsType<InvalidOperationException>(reload.Error);
        Assert.Null(reload.NewShell);
        Assert.Null(reload.Drain);
        Assert.Same(active, registry.GetActive("payments"));
        Assert.Equal(ShellLifecycleState.Active, active.State);
        Assert.Equal(["prepare"], probe.Events);
    }

    [Theory(DisplayName = "No-op preparation and no preparer preserve typed configuration and code-first overrides")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preparation_Unchanged_PreservesLegacyConfiguration(bool usePreparer)
    {
        var typed = new object();
        var probe = new PreparationProbe();
        var preparer = usePreparer ? new FirstPreparer() : null;
        await using var host = BuildPreparedHost(probe, preparer, cshells => cshells
            .AddShell("payments", shell => shell
                .WithConfiguration("Typed", typed)
                .WithConfiguration("PreparationDependent:Value", "authored")
                .WithFeature<PreparationDependentFeature>(feature =>
                {
                    probe.Events.Add($"configurator:{feature.Value}");
                    feature.Value = "code";
                })));

        var shell = await host.GetRequiredService<IShellRegistry>().ActivateAsync("payments");

        Assert.Same(typed, shell.ServiceProvider.GetRequiredService<ShellSettings>().GetConfiguration<object>("Typed"));
        Assert.Contains("configure:authored", probe.Events);
        Assert.Contains("configurator:authored", probe.Events);
        Assert.Equal("services:PreparationDependent:code", probe.Events.Last());
        Assert.Null(shell.ServiceProvider.GetService<IShellSettingsPreparer>());
    }

    [Fact(DisplayName = "Preparation patches change only the selected shell and preserve unknown authored settings")]
    public async Task Preparation_AppliesScalarPatch_IsolatesShellsAndAuthoredSettings()
    {
        var probe = new PreparationProbe();
        var typed = new object();
        var preparer = new RecordingPreparer(probe, context =>
        {
            Assert.Equal("authored", context.ConfigurationData["Shared"]);
            return new ShellSettingsPreparationResult(new Dictionary<string, string?>
            {
                ["Shared"] = context.ShellId.Name,
                ["Remove"] = null,
                ["Disabled"] = "false",
                ["Count"] = "0"
            });
        });
        await using var host = BuildPreparedHost(probe, preparer, cshells => cshells
            .ConfigureAllShells(shell => shell
                .WithConfiguration("Shared", "authored")
                .WithConfiguration("Remove", "old")
                .WithConfiguration("Unknown:Typed", typed))
            .AddShell("one", _ => { })
            .AddShell("two", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();
        var first = await registry.ActivateAsync("one");
        var second = await registry.ActivateAsync("two");

        foreach (var shell in new[] { first, second })
        {
            var settings = shell.ServiceProvider.GetRequiredService<ShellSettings>();
            Assert.Equal(shell.Descriptor.Name, settings.GetConfiguration("Shared"));
            Assert.False(settings.ConfigurationData.ContainsKey("Remove"));
            Assert.Equal("false", settings.GetConfiguration("Disabled"));
            Assert.Equal("0", settings.GetConfiguration("Count"));
            Assert.Same(typed, settings.ConfigurationData["Unknown:Typed"]);
        }
        Assert.Equal(["prepare", "prepare"], probe.Events);
    }

    private static ServiceProvider BuildPreparedHost(
        PreparationProbe probe,
        IShellSettingsPreparer? preparer,
        Action<CShellsBuilder>? configure = null) => ShellRegistryActivateTests.BuildHost(
        cshells =>
        {
            cshells.WithAssemblyContaining<ShellSettingsPreparationTests>();
            if (configure is null)
                cshells.AddShell("payments", shell => shell.WithFeature<PreparationDependentFeature>());
            else
                configure(cshells);
        },
        services =>
        {
            services.AddSingleton(probe);
            if (preparer is not null)
                services.AddSingleton<IShellSettingsPreparer>(preparer);
        });

    private sealed class RecordingPreparer(
        PreparationProbe probe,
        Func<ShellSettingsPreparationContext, ShellSettingsPreparationResult> prepare) : IShellSettingsPreparer
    {
        public Task<ShellSettingsPreparationResult> PrepareAsync(
            ShellSettingsPreparationContext context,
            CancellationToken cancellationToken = default)
        {
            probe.Events.Add("prepare");
            return Task.FromResult(prepare(context));
        }
    }

    private sealed class FirstPreparer : IShellSettingsPreparer
    {
        public Task<ShellSettingsPreparationResult> PrepareAsync(
            ShellSettingsPreparationContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ShellSettingsPreparationResult.Unchanged(context));
    }

    public sealed class PreparationProbe
    {
        public List<string> Events { get; } = [];
    }

    public sealed class PreparationOptions
    {
        public string? Value { get; set; }
    }

    [ShellFeature("PreparationDependency")]
    public sealed class PreparationDependencyFeature : IShellFeature
    {
        private readonly PreparationProbe probe;

        public PreparationDependencyFeature(PreparationProbe probe)
        {
            this.probe = probe;
            probe.Events.Add("ctor:PreparationDependency");
        }

        public string? Value { get; set; }

        public void ConfigureServices(IServiceCollection services) => probe.Events.Add($"services:PreparationDependency:{Value}");
    }

    [ShellFeature("PreparationDependent", DependsOn = ["PreparationDependency"])]
    public sealed class PreparationDependentFeature : IShellFeature, IConfigurableFeature<PreparationOptions>
    {
        private readonly PreparationProbe probe;

        public PreparationDependentFeature(PreparationProbe probe)
        {
            this.probe = probe;
            probe.Events.Add("ctor:PreparationDependent");
        }

        public string? Value { get; set; }

        public void Configure(PreparationOptions options)
        {
            Value = options.Value;
            probe.Events.Add($"configure:{Value}");
        }

        public void ConfigureServices(IServiceCollection services) => probe.Events.Add($"services:PreparationDependent:{Value}");
    }

    private sealed class ReusableSettingsBlueprint : IShellBlueprint
    {
        private readonly ShellSettings settings;

        public ReusableSettingsBlueprint(string name)
        {
            Name = name;
            TypedValue = new object();
            settings = new ShellSettings(new ShellId(name));
            settings.ConfigurationData["Typed"] = TypedValue;
        }

        public string Name { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
        public object TypedValue { get; }

        public Task<ShellSettings> ComposeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);
    }
}
