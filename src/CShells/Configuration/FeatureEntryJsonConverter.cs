using System.Text.Json;
using System.Text.Json.Serialization;

namespace CShells.Configuration;

/// <summary>
/// JSON converter for <see cref="FeatureEntry"/> that supports both string and object formats.
/// </summary>
/// <remarks>
/// <para>
/// This converter enables a polymorphic Features array in configuration where each element
/// can be either:
/// </para>
/// <list type="bullet">
///   <item><description>A simple string: <c>"FeatureName"</c></description></item>
///   <item><description>An object with Name and settings: <c>{ "Name": "FeatureName", "Setting1": "Value1" }</c></description></item>
/// </list>
/// <para>
/// In the object format, all properties except "Name" are treated as feature settings.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// {
///   "Features": [
///     "Core",
///     { "Name": "FraudDetection", "Threshold": 0.85, "MaxTransactionAmount": 5000 },
///     "EmailNotification"
///   ]
/// }
/// </code>
/// </example>
public class FeatureEntryJsonConverter : JsonConverter<FeatureEntry>
{
    private const string NameProperty = "Name";

    /// <inheritdoc/>
    public override FeatureEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => ReadFromString(ref reader),
            JsonTokenType.StartObject => ReadFromObject(ref reader),
            _ => throw new JsonException($"Feature entry must be a string or object, but found {reader.TokenType}")
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, FeatureEntry value, JsonSerializerOptions options)
    {
        if (value.Settings.Count == 0)
        {
            // Write as simple string when no settings
            writer.WriteStringValue(value.Name);
        }
        else
        {
            // Write as object with Name and settings
            writer.WriteStartObject();
            writer.WriteString(NameProperty, value.Name);

            foreach (var (key, settingValue) in value.Settings)
            {
                writer.WritePropertyName(key);
                JsonSerializer.Serialize(writer, settingValue, options);
            }

            writer.WriteEndObject();
        }
    }

    private static FeatureEntry ReadFromString(ref Utf8JsonReader reader)
    {
        var name = reader.GetString();

        if (string.IsNullOrWhiteSpace(name))
            throw new JsonException("Feature name cannot be null or empty");

        return FeatureEntry.FromName(name.Trim());
    }

    private static FeatureEntry ReadFromObject(ref Utf8JsonReader reader)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (!root.TryGetProperty(NameProperty, out var nameElement) &&
            !root.TryGetProperty("name", out nameElement))
        {
            throw new JsonException($"Feature object must have a '{NameProperty}' property");
        }

        var name = nameElement.GetString();

        if (string.IsNullOrWhiteSpace(name))
            throw new JsonException("Feature name cannot be null or empty");

        var entry = new FeatureEntry { Name = name.Trim() };

        // All other properties are settings
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals(NameProperty, StringComparison.OrdinalIgnoreCase))
                continue;

            // Clone the JsonElement to keep it alive after document disposal
            entry.Settings[property.Name] = CloneJsonElement(property.Value);
        }

        return entry;
    }

    private static object? CloneJsonElement(JsonElement element)
    {
        // Convert JsonElement to a native type for easier handling
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            // For complex types (arrays, objects), keep as JsonElement by re-parsing
            _ => JsonSerializer.Deserialize<JsonElement>(element.GetRawText())
        };
    }
}



