using System.Collections.Immutable;
using CShells.Configuration;
using CShells.Lifecycle;
using Microsoft.Extensions.Configuration;

namespace CShells.Lifecycle.Blueprints;

/// <summary>
/// Configuration-backed <see cref="IShellBlueprint"/>. Re-reads the bound section on every
/// <see cref="ComposeAsync"/> call so configuration edits between reloads are picked up.
/// </summary>
/// <remarks>
/// Accepts the <see cref="Features"/> entry both as a JSON string array
/// (<c>"Features": ["Core", "Posts"]</c>) and as an array of objects
/// (<c>"Features": [ { "Name": "Core" }, { "Name": "Analytics", "Settings": { "Top": 10 } } ]</c>),
/// plus an optional <see cref="Configuration"/> subsection whose keys flow into
/// <see cref="ShellSettings.ConfigurationData"/>.
/// </remarks>
public sealed class ConfigurationShellBlueprint : IShellBlueprint
{
    private readonly IConfiguration _section;

    public ConfigurationShellBlueprint(string name, IConfiguration section, IReadOnlyDictionary<string, string>? metadata = null)
    {
        Name = Guard.Against.NullOrWhiteSpace(name);
        _section = Guard.Against.Null(section);
        Metadata = metadata is null
            ? ImmutableDictionary<string, string>.Empty
            : ImmutableDictionary.CreateRange(metadata);
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <inheritdoc />
    public Task<ShellSettings> ComposeAsync(CancellationToken cancellationToken = default)
    {
        var sourceGeneration = _section.GetReloadToken();
        var settings = new ShellSettings(new ShellId(Name));

        var configurationSection = _section.GetSection("Configuration");
        ConfigurationHelper.LoadConfigurationFromSection(configurationSection, settings.ConfigurationData);

        var featuresSection = _section.GetSection("Features");
        var features = ConfigurationHelper.ParseFeaturesFromConfiguration(featuresSection, Name);
        ConfigurationHelper.ApplyFeatureEntries(features, settings);

        if (sourceGeneration.HasChanged)
            throw ShellConfigurationGeneration.CreateChangedException(Name, "while composing");

        ShellConfigurationGeneration.Attach(settings, sourceGeneration);

        return Task.FromResult(settings);
    }
}
