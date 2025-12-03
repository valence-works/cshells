using System.Text.Json;
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
        var normalizedFeatures = config.Features
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f!.Trim())
            .ToArray();
        var settings = new ShellSettings(shellId, normalizedFeatures);

        // Convert property values to JsonElement for consistent serialization
        foreach (var property in config.Properties)
        {
            var converted = ConvertToJsonElement(property.Value);
            if (converted != null)
                settings.Properties[property.Key] = converted;
        }

        return settings;
    }

    /// <summary>
    /// Converts a value to JsonElement for consistent serialization.
    /// </summary>
    private static object? ConvertToJsonElement(object? value)
    {
        if (value == null)
            return null;

        // Already a JsonElement, return as-is
        if (value is JsonElement)
            return value;

        // For primitives and strings, serialize to JsonElement
        if (value is string || value.GetType().IsPrimitive)
        {
            var json = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        // For complex objects, serialize and deserialize to JsonElement
        // Use options that handle complex nested structures
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };

            var json = JsonSerializer.Serialize(value, value.GetType(), options);
            return JsonSerializer.Deserialize<JsonElement>(json, options);
        }
        catch
        {
            // If serialization fails, return the original value
            return value;
        }
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
        var features = section.GetSection("Features").Get<string[]>() ?? [];

        var shellId = new ShellId(name);
        var normalizedFeatures = features
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f.Trim())
            .ToArray();
        var settings = new ShellSettings(shellId, normalizedFeatures);

        // Manually bind properties from configuration
        var propertiesSection = section.GetSection("Properties");
        foreach (var propertySection in propertiesSection.GetChildren())
        {
            var key = propertySection.Key;

            // Check if this is a complex object or a simple value
            if (propertySection.GetChildren().Any())
            {
                // Complex object - serialize to JSON and store as JsonElement
                var json = SerializeConfigurationSection(propertySection);
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
                settings.Properties[key] = jsonElement;
            }
            else
            {
                // Simple value
                var value = propertySection.Value;
                var converted = ConvertToJsonElement(value);
                if (converted != null)
                    settings.Properties[key] = converted;
            }
        }

        // Process shell-specific settings (configuration data)
        var settingsSection = section.GetSection("Settings");
        foreach (var settingSection in settingsSection.GetChildren())
        {
            var key = settingSection.Key;

            // Check if this is a complex object or a simple value
            if (settingSection.GetChildren().Any())
            {
                // Complex object - flatten to key-value pairs for IConfiguration
                FlattenConfigurationSection(settingSection, key, settings.ConfigurationData);
            }
            else
            {
                // Simple value
                settings.ConfigurationData[key] = settingSection.Value!;
            }
        }

        return settings;
    }

    /// <summary>
    /// Flattens a configuration section into key-value pairs suitable for IConfiguration.
    /// </summary>
    private static void FlattenConfigurationSection(IConfigurationSection section, string prefix, IDictionary<string, object> target)
    {
        foreach (var child in section.GetChildren())
        {
            var key = $"{prefix}:{child.Key}";

            if (child.GetChildren().Any())
            {
                // Recursively flatten nested sections
                FlattenConfigurationSection(child, key, target);
            }
            else
            {
                target[key] = child.Value!;
            }
        }
    }

    /// <summary>
    /// Serializes an IConfigurationSection to JSON string.
    /// </summary>
    private static string SerializeConfigurationSection(IConfigurationSection section)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var child in section.GetChildren())
        {
            if (child.GetChildren().Any())
            {
                // Nested object
                dict[child.Key] = JsonSerializer.Deserialize<JsonElement>(SerializeConfigurationSection(child));
            }
            else
            {
                // Simple value
                dict[child.Key] = child.Value;
            }
        }

        return JsonSerializer.Serialize(dict);
    }
}
