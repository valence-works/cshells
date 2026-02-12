using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CShells.Configuration;

/// <summary>
/// Shared helper methods for processing configuration data into ShellSettings.
/// </summary>
internal static class ConfigurationHelper
{
    /// <summary>
    /// Converts a value to JsonElement for consistent serialization.
    /// </summary>
    public static object? ConvertToJsonElement(object? value)
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
    /// Flattens a configuration section into key-value pairs suitable for IConfiguration.
    /// </summary>
    public static void FlattenConfigurationSection(IConfigurationSection section, string prefix, IDictionary<string, object> target)
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
    public static string SerializeConfigurationSection(IConfigurationSection section)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var child in section.GetChildren())
        {
            var value = child.GetChildren().Any()
                ? (object?)JsonSerializer.Deserialize<JsonElement>(SerializeConfigurationSection(child))
                : child.Value;

            dict[child.Key] = value;
        }

        return JsonSerializer.Serialize(dict);
    }

    /// <summary>
    /// Loads properties from a configuration section into a dictionary.
    /// </summary>
    public static void LoadPropertiesFromConfiguration(IConfigurationSection propertiesSection, IDictionary<string, object> targetProperties)
    {
        foreach (var propertySection in propertiesSection.GetChildren())
        {
            var key = propertySection.Key;

            // Check if this is a complex object or a simple value
            if (propertySection.GetChildren().Any())
            {
                // Complex object - serialize to JSON and store as JsonElement
                var json = SerializeConfigurationSection(propertySection);
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
                targetProperties[key] = jsonElement;
            }
            else
            {
                // Simple value
                var value = propertySection.Value;
                var converted = ConvertToJsonElement(value);
                if (converted != null)
                    targetProperties[key] = converted;
            }
        }
    }

    /// <summary>
    /// Loads settings (configuration data) from a configuration section into a dictionary.
    /// </summary>
    public static void LoadSettingsFromConfiguration(IConfigurationSection settingsSection, IDictionary<string, object> targetConfigurationData)
    {
        foreach (var settingSection in settingsSection.GetChildren())
        {
            var key = settingSection.Key;

            // Check if this is a complex object or a simple value
            if (settingSection.GetChildren().Any())
            {
                // Complex object - flatten to key-value pairs for IConfiguration
                FlattenConfigurationSection(settingSection, key, targetConfigurationData);
            }
            else
            {
                // Simple value
                targetConfigurationData[key] = settingSection.Value!;
            }
        }
    }

    /// <summary>
    /// Normalizes and retrieves features from a configuration section.
    /// </summary>
    public static string[] GetNormalizedFeatures(IConfigurationSection section)
    {
        var features = section.GetSection("Features").Get<string[]>() ?? [];
        return features
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f.Trim())
            .ToArray();
    }

    /// <summary>
    /// Normalizes features from a string array.
    /// </summary>
    public static string[] NormalizeFeatures(IEnumerable<string?> features)
    {
        return features
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f!.Trim())
            .ToArray();
    }
}
