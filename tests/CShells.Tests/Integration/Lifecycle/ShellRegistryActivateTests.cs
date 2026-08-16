using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using CShells.Lifecycle.Blueprints;
using CShells.Lifecycle.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CShells.Tests.Integration.Lifecycle;

public class ShellRegistryActivateTests
{
    [Fact(DisplayName = "ActivateAsync stamps generation 1 and promotes to Active")]
    public async Task ActivateAsync_StampsGeneration1_AndPromotesToActive()
    {
        await using var host = BuildHost(cshells => cshells
            .WithAssemblies() // explicit empty — no feature discovery
            .AddShell("payments", _ => { }));

        var registry = host.GetRequiredService<IShellRegistry>();

        var shell = await registry.ActivateAsync("payments");

        Assert.Equal("payments", shell.Descriptor.Name);
        Assert.Equal(1, shell.Descriptor.Generation);
        Assert.Equal(ShellLifecycleState.Active, shell.State);
        Assert.Same(shell, registry.GetActive("payments"));
        Assert.Equal([shell], registry.GetAll("payments"));
    }

    [Fact(DisplayName = "ActivateAsync on a name with no blueprint throws ShellBlueprintNotFoundException")]
    public async Task ActivateAsync_WithoutBlueprint_Throws()
    {
        await using var host = BuildHost(cshells => cshells.WithAssemblies());
        var registry = host.GetRequiredService<IShellRegistry>();

        var ex = await Assert.ThrowsAsync<ShellBlueprintNotFoundException>(() => registry.ActivateAsync("unknown"));
        Assert.Equal("unknown", ex.Name);
    }

    [Fact(DisplayName = "ActivateAsync twice on the same name throws (caller should use ReloadAsync)")]
    public async Task ActivateAsync_WhenAlreadyActive_Throws()
    {
        await using var host = BuildHost(cshells => cshells
            .WithAssemblies()
            .AddShell("payments", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();

        await registry.ActivateAsync("payments");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => registry.ActivateAsync("payments"));
        Assert.Contains("Active", ex.Message);
        Assert.Contains("ReloadAsync", ex.Message);
    }

    [Fact(DisplayName = "Duplicate blueprint registration in the in-memory provider throws")]
    public void DuplicateBlueprint_Throws()
    {
        // The in-memory provider is self-contained; no host needed to assert its duplicate guard.
        var provider = new InMemoryShellBlueprintProvider();
        provider.Add(new DelegateShellBlueprint("payments", _ => { }));

        Assert.Throws<InvalidOperationException>(() =>
            provider.Add(new DelegateShellBlueprint("Payments", _ => { })));
    }

    [Fact(DisplayName = "Blueprint composition exception propagates and leaves no partial entry")]
    public async Task CompositionException_Propagates_NoPartialEntry()
    {
        await using var host = BuildHost(cshells => cshells
            .WithAssemblies()
            .AddBlueprint(new ThrowingBlueprint("payments")));
        var registry = host.GetRequiredService<IShellRegistry>();

        await Assert.ThrowsAsync<ApplicationException>(() => registry.ActivateAsync("payments"));

        Assert.Null(registry.GetActive("payments"));
        Assert.Empty(registry.GetAll("payments"));
    }

    [Fact(DisplayName = "Endpoint publication failure aborts the candidate generation and preserves no partial entry")]
    public async Task GenerationPublicationFailure_DisposesCandidate_NoPartialEntry()
    {
        await using var host = BuildHost(
            cshells => cshells
                .WithAssemblies()
                .AddShell("payments", _ => { }),
            services => services.AddSingleton<IShellLifecycleSubscriber, RejectingActivationSubscriber>());
        var registry = host.GetRequiredService<IShellRegistry>();

        await Assert.ThrowsAsync<ShellGenerationActivationException>(() => registry.ActivateAsync("payments"));

        Assert.Null(registry.GetActive("payments"));
        Assert.Empty(registry.GetAll("payments"));
    }

    [Fact(DisplayName = "Blueprint name mismatch in composed settings throws")]
    public async Task Blueprint_NameMismatch_Throws()
    {
        await using var host = BuildHost(cshells => cshells
            .WithAssemblies()
            .AddBlueprint(new NameMismatchBlueprint("payments")));
        var registry = host.GetRequiredService<IShellRegistry>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => registry.ActivateAsync("payments"));
        Assert.Contains("blueprint name mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "IShell is resolvable from the shell's own provider")]
    public async Task IShell_IsResolvable_FromShellProvider()
    {
        await using var host = BuildHost(cshells => cshells
            .WithAssemblies()
            .AddShell("payments", _ => { }));
        var registry = host.GetRequiredService<IShellRegistry>();

        var shell = await registry.ActivateAsync("payments");
        var resolved = shell.ServiceProvider.GetRequiredService<IShell>();

        Assert.Same(shell, resolved);
    }

    [Fact(DisplayName = "Activation expands feature dependencies in ShellSettings")]
    public async Task ActivateAsync_DependencyFeatures_AreExpandedInShellSettings()
    {
        await using var host = BuildHost(cshells => cshells
            .WithAssemblyContaining<ShellRegistryActivateTests>()
            .AddShell("payments", shell => shell.WithFeatures(typeof(DependencyExpansionDependentFeature))));
        var registry = host.GetRequiredService<IShellRegistry>();

        var shell = await registry.ActivateAsync("payments");
        var settings = shell.ServiceProvider.GetRequiredService<ShellSettings>();

        Assert.Equal(
            ["DependencyExpansionDependency", "DependencyExpansionDependent"],
            settings.EnabledFeatures);
        Assert.NotNull(shell.ServiceProvider.GetService<DependencyExpansionMarker>());
    }

    [Fact(DisplayName = "Activation warns about missing positive feature names and uses available features")]
    public async Task ActivateAsync_MissingFeatures_WarnsAndUsesAvailableFeatures()
    {
        var logs = new List<(LogLevel Level, string Message)>();
        await using var host = BuildHost(
            cshells => cshells
                .WithAssemblyContaining<ShellRegistryActivateTests>()
                .AddShell("payments", shell => shell.WithFeatures("DependencyExpansionDependent", "MissingFeature")),
            services => services.AddSingleton<ILogger<ShellProviderBuilder>>(new CapturingLogger<ShellProviderBuilder>(logs)));
        var registry = host.GetRequiredService<IShellRegistry>();

        var shell = await registry.ActivateAsync("payments");
        var settings = shell.ServiceProvider.GetRequiredService<ShellSettings>();

        Assert.Equal(
            ["DependencyExpansionDependency", "DependencyExpansionDependent"],
            settings.EnabledFeatures);
        Assert.Contains(logs, entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("MissingFeature", StringComparison.Ordinal) &&
            entry.Message.Contains("available features only", StringComparison.Ordinal));
    }

    internal static ServiceProvider BuildHost(Action<CShellsBuilder> configure, Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        configureServices?.Invoke(services);
        services.AddCShells(configure);
        return services.BuildServiceProvider();
    }

    private sealed class ThrowingBlueprint(string name) : IShellBlueprint
    {
        public string Name { get; } = name;
        public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

        public Task<ShellSettings> ComposeAsync(CancellationToken cancellationToken = default)
            => throw new ApplicationException("compose fail");
    }

    private sealed class NameMismatchBlueprint(string name) : IShellBlueprint
    {
        public string Name { get; } = name;
        public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

        public Task<ShellSettings> ComposeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ShellSettings(new ShellId("other-name")));
    }

    private sealed class CapturingLogger<T>(List<(LogLevel Level, string Message)> sink) : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            sink.Add((logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class RejectingActivationSubscriber : IShellLifecycleSubscriber
    {
        public Task OnStateChangedAsync(
            IShell shell,
            ShellLifecycleState previous,
            ShellLifecycleState current,
            CancellationToken cancellationToken = default) =>
            current == ShellLifecycleState.Active
                ? throw new ShellGenerationActivationException(shell.Descriptor, new InvalidOperationException("candidate rejected"))
                : Task.CompletedTask;
    }
}

[ShellFeature("DependencyExpansionDependency")]
public sealed class DependencyExpansionDependencyFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<DependencyExpansionMarker>();
    }
}

[ShellFeature("DependencyExpansionDependent", DependsOn = ["DependencyExpansionDependency"])]
public sealed class DependencyExpansionDependentFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
    }
}

public sealed class DependencyExpansionMarker;
