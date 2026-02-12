using Microsoft.Extensions.Configuration;

namespace CShells.Configuration;

/// <summary>
/// Transforms configuration models into runtime ShellSettings instances.
/// </summary>
public static class ShellSettingsFactory
{
    /// <summary>
    /// Creates a <see cref="ShellSettings"/> instance from a <see cref="ShellConfig"/>.
    /// </summary>
    /// <param name="config">The shell configuration.</param>
    /// <returns>A new <see cref="ShellSettings"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public static ShellSettings Create(ShellConfig config)
    {
        Guard.Against.Null(config);

        var shellId = new ShellId(config.Name);
        var normalizedFeatures = ConfigurationHelper.NormalizeFeatures(config.Features);
        var settings = new ShellSettings(shellId, normalizedFeatures);

        // Convert property values to JsonElement for consistent serialization
        foreach (var property in config.Properties)
        {
            var converted = ConfigurationHelper.ConvertToJsonElement(property.Value);
            if (converted != null)
                settings.Properties[property.Key] = converted;
        }

        // Populate configuration data from settings
        foreach (var setting in config.Settings.Where(s => s.Value != null))
        {
            settings.ConfigurationData[setting.Key] = setting.Value!;
        }

        return settings;
    }

    /// <summary>
    /// Creates a collection of <see cref="ShellSettings"/> instances from <see cref="CShellsOptions"/>.
    /// </summary>
    /// <param name="options">The CShells options.</param>
    /// <returns>A collection of <see cref="ShellSettings"/> instances.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public static IReadOnlyList<ShellSettings> CreateAll(CShellsOptions options)
    {
        Guard.Against.Null(options);

        var duplicates = (options.Shells
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)).ToArray();

        if (duplicates.Any())
        {
            throw new ArgumentException($"Duplicate shell name: {duplicates.First()}", nameof(options));
        }
        return options.Shells.Select(Create).ToList();
    }

    /// <summary>
    /// Creates a collection of <see cref="ShellSettings"/> instances from <see cref="CShellsOptions"/>.
    /// This is an alias for <see cref="CreateAll"/>.
    /// </summary>
    /// <param name="options">The CShells options.</param>
    /// <returns>A collection of <see cref="ShellSettings"/> instances.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when duplicate shell names are found.</exception>
    public static IReadOnlyList<ShellSettings> CreateFromOptions(CShellsOptions options) => CreateAll(options);

    /// <summary>
    /// Creates a <see cref="ShellSettings"/> instance directly from an IConfigurationSection.
    /// This method properly handles nested property sections like WebRoutingShellOptions.
    /// </summary>
    /// <param name="section">The configuration section representing a shell.</param>
    /// <returns>A new <see cref="ShellSettings"/> instance.</returns>
    public static ShellSettings CreateFromConfiguration(IConfigurationSection section)
    {
        Guard.Against.Null(section);

        var name = section.GetValue<string>("Name") ?? throw new InvalidOperationException("Shell name is required");
        var normalizedFeatures = ConfigurationHelper.GetNormalizedFeatures(section);

        var shellId = new ShellId(name);
        var settings = new ShellSettings(shellId, normalizedFeatures);

        // Load properties from configuration
        var propertiesSection = section.GetSection("Properties");
        ConfigurationHelper.LoadPropertiesFromConfiguration(propertiesSection, settings.Properties);

        // Load shell-specific settings (configuration data)
        var settingsSection = section.GetSection("Settings");
        ConfigurationHelper.LoadSettingsFromConfiguration(settingsSection, settings.ConfigurationData);

        return settings;
    }
}
