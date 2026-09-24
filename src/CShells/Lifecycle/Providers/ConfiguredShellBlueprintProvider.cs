using CShells.Configuration;
using CShells.Lifecycle;

namespace CShells.Lifecycle.Providers;

internal sealed class ConfiguredShellBlueprintProvider(
    IShellBlueprintProvider inner,
    IReadOnlyList<Action<ShellBuilder>> shellConfigurators) : IShellBlueprintProvider
{
    private readonly IShellBlueprintProvider inner = Guard.Against.Null(inner);
    private readonly IReadOnlyList<Action<ShellBuilder>> shellConfigurators = Guard.Against.Null(shellConfigurators);

    public async Task<ProvidedBlueprint?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        var provided = await inner.GetAsync(name, cancellationToken).ConfigureAwait(false);
        return provided is null
            ? null
            : new ProvidedBlueprint(new ConfiguredShellBlueprint(provided.Blueprint, shellConfigurators), provided.Manager);
    }

    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default) =>
        inner.ExistsAsync(name, cancellationToken);

    // BlueprintPage carries summaries only; callers load concrete blueprints through GetAsync,
    // which wraps them before any ComposeAsync call observes shell settings.
    public Task<BlueprintPage> ListAsync(BlueprintListQuery query, CancellationToken cancellationToken = default) =>
        inner.ListAsync(query, cancellationToken);

    private sealed class ConfiguredShellBlueprint(
        IShellBlueprint inner,
        IReadOnlyList<Action<ShellBuilder>> shellConfigurators) : IShellBlueprint
    {
        private readonly IShellBlueprint inner = Guard.Against.Null(inner);
        private readonly IReadOnlyList<Action<ShellBuilder>> shellConfigurators = Guard.Against.Null(shellConfigurators);

        public string Name => inner.Name;

        public IReadOnlyDictionary<string, string> Metadata => inner.Metadata;

        public async Task<ShellSettings> ComposeAsync(CancellationToken cancellationToken = default)
        {
            var settings = await inner.ComposeAsync(cancellationToken).ConfigureAwait(false);
            if (shellConfigurators.Count == 0)
                return settings;

            var defaultsBuilder = new ShellBuilder(settings.Id);
            foreach (var configurator in shellConfigurators)
                configurator(defaultsBuilder);

            return ShellSettingsMerger.Merge(defaultsBuilder.Build(), settings);
        }
    }

    private static class ShellSettingsMerger
    {
        public static ShellSettings Merge(ShellSettings defaults, ShellSettings shellSpecific)
        {
            var merged = new ShellSettings(shellSpecific.Id)
            {
                EnabledFeatures = defaults.EnabledFeatures,
                DisabledFeatures = defaults.DisabledFeatures,
                FeatureSettingResets = defaults.FeatureSettingResets,
                ConfigurationData = new Dictionary<string, object>(
                    defaults.ConfigurationData,
                    StringComparer.OrdinalIgnoreCase)
            };

            foreach (var (key, value) in shellSpecific.ConfigurationData)
                merged.ConfigurationData[key] = value;

            ApplyFeatureDeclarations(merged, shellSpecific);

            foreach (var (featureName, configure) in defaults.FeatureConfigurators)
                merged.FeatureConfigurators[featureName] = configure;

            foreach (var (featureName, configure) in shellSpecific.FeatureConfigurators)
            {
                if (merged.FeatureConfigurators.TryGetValue(featureName, out var existing))
                    merged.FeatureConfigurators[featureName] = ChainConfigurators(existing, configure);
                else
                    merged.FeatureConfigurators[featureName] = configure;
            }

            ShellConfigurationGeneration.Copy(shellSpecific, merged);

            return merged;
        }

        private static void ApplyFeatureDeclarations(ShellSettings target, ShellSettings shellSpecific)
        {
            foreach (var featureName in shellSpecific.DisabledFeatures)
                ConfigurationHelper.DisableFeature(target, featureName);

            foreach (var featureName in shellSpecific.EnabledFeatures)
                ConfigurationHelper.AddEnabledFeature(
                    target,
                    featureName,
                    shellSpecific.FeatureSettingResets.Contains(featureName, StringComparer.OrdinalIgnoreCase));
        }

        private static Action<T> ChainConfigurators<T>(Action<T> first, Action<T> second) =>
            target =>
            {
                first(target);
                second(target);
            };

    }
}
