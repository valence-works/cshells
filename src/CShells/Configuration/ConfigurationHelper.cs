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
    /// Flattens a dictionary of settings into key-value pairs suitable for IConfiguration.
    /// </summary>
    public static void FlattenSettings(Dictionary<string, object?> settings, string prefix, IDictionary<string, object> target)
    {
        foreach (var (key, value) in settings)
        {
            if (value == null)
                continue;

            var fullKey = $"{prefix}:{key}";

            if (value is JsonElement jsonElement)
            {
                FlattenJsonElement(jsonElement, fullKey, target);
            }
            else if (value is Dictionary<string, object?> nested)
            {
                FlattenSettings(nested, fullKey, target);
            }
            else
            {
                target[fullKey] = value;
            }
        }
    }

    /// <summary>
    /// Flattens a JsonElement into key-value pairs suitable for IConfiguration.
    /// </summary>
    public static void FlattenJsonElement(JsonElement element, string prefix, IDictionary<string, object> target)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    FlattenJsonElement(property.Value, $"{prefix}:{property.Name}", target);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenJsonElement(item, $"{prefix}:{index}", target);
                    index++;
                }
                break;

            case JsonValueKind.String:
                var stringValue = element.GetString();
                if (stringValue != null)
                {
                    target[prefix] = stringValue;
                }
                break;

            case JsonValueKind.Number:
                target[prefix] = element.GetRawText();
                break;

            case JsonValueKind.True:
                target[prefix] = "True";
                break;

            case JsonValueKind.False:
                target[prefix] = "False";
                break;

            case JsonValueKind.Null:
                // Skip null values
                break;
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
    /// Loads shell configuration from a configuration section into ConfigurationData.
    /// Complex objects are flattened using colon-separated keys for IConfiguration compatibility.
    /// </summary>
    public static void LoadConfigurationFromSection(IConfigurationSection configSection, IDictionary<string, object> targetConfigurationData)
    {
        foreach (var section in configSection.GetChildren())
        {
            var key = section.Key;

            // Check if this is a complex object or a simple value
            if (section.GetChildren().Any())
            {
                // Complex object - flatten to key-value pairs for IConfiguration
                FlattenConfigurationSection(section, key, targetConfigurationData);
            }
            else
            {
                // Simple value
                if (section.Value != null)
                    targetConfigurationData[key] = section.Value;
            }
        }
    }

    /// <summary>
    /// Extracts feature names from a list of feature entries.
    /// </summary>
    public static string[] ExtractFeatureNames(IEnumerable<FeatureEntry> features)
    {
        return features
            .Where(f => !string.IsNullOrWhiteSpace(f.Name))
            .Select(f => f.Name.Trim())
            .ToArray();
    }

    /// <summary>
    /// Populates configuration data from feature entries.
    /// Settings from each feature are namespaced under the feature name.
    /// </summary>
    public static void PopulateFeatureSettings(IEnumerable<FeatureEntry> features, IDictionary<string, object> configurationData)
    {
        foreach (var feature in features)
        {
            if (feature.Settings.Count == 0)
                continue;

            FlattenSettings(feature.Settings, feature.Name, configurationData);
        }
    }

    /// <summary>
    /// Populates shell configuration from a dictionary into ConfigurationData.
    /// Complex objects are flattened using colon-separated keys for IConfiguration compatibility.
    /// </summary>
    public static void PopulateShellConfiguration(Dictionary<string, object?> configuration, IDictionary<string, object> configurationData)
    {
        foreach (var (key, value) in configuration)
        {
            if (value == null)
                continue;

            if (value is JsonElement jsonElement)
            {
                FlattenJsonElement(jsonElement, key, configurationData);
            }
            else if (value is Dictionary<string, object?> nested)
            {
                PopulateShellConfiguration(nested, configurationData);
            }
            else
            {
                configurationData[key] = value;
            }
        }
    }

    /// <summary>
    /// Parses feature entries from a configuration section.
    /// Handles both string and object formats in the Features array.
    /// </summary>
    public static List<FeatureEntry> ParseFeaturesFromConfiguration(IConfigurationSection featuresSection)
    {
        var entries = new List<FeatureEntry>();

        foreach (var featureSection in featuresSection.GetChildren())
        {
            // Check if this is a simple string value
            if (!featureSection.GetChildren().Any())
            {
                var name = featureSection.Value;
                if (!string.IsNullOrWhiteSpace(name))
                    entries.Add(FeatureEntry.FromName(name.Trim()));
            }
            else
            {
                // This is an object with Name and settings
                var name = featureSection["Name"];
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var entry = new FeatureEntry { Name = name.Trim() };

                // All other children are settings
                foreach (var settingSection in featureSection.GetChildren())
                {
                    if (settingSection.Key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (settingSection.GetChildren().Any())
                    {
                        // Complex nested setting - store as JsonElement
                        var json = SerializeConfigurationSection(settingSection);
                        entry.Settings[settingSection.Key] = JsonSerializer.Deserialize<JsonElement>(json);
                    }
                    else
                    {
                        // Simple value
                        entry.Settings[settingSection.Key] = settingSection.Value;
                    }
                }

                entries.Add(entry);
            }
        }

        return entries;
    }
}
