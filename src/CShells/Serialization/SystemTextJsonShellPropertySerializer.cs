using System.Text.Json;

namespace CShells.Serialization;

/// <summary>
/// Default implementation of <see cref="IShellPropertySerializer"/> using System.Text.Json.
/// </summary>
public class SystemTextJsonShellPropertySerializer : IShellPropertySerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemTextJsonShellPropertySerializer"/> class.
    /// </summary>
    /// <param name="options">The JSON serializer options to use. If null, default options are used.</param>
    public SystemTextJsonShellPropertySerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc />
    public T? Deserialize<T>(object? value)
    {
        return (T?)Deserialize(value, typeof(T));
    }

    /// <inheritdoc />
    public object? Deserialize(object? value, Type targetType)
    {
        Guard.Against.Null(targetType);

        if (value == null)
            return null;

        // If value is already the target type, return it
        if (targetType.IsInstanceOfType(value))
            return value;

        // Handle JsonElement (from appsettings.json)
        if (value is JsonElement jsonElement)
        {
            // For string targets, try to get string directly
            if (targetType == typeof(string))
            {
                return jsonElement.ValueKind == JsonValueKind.String
                    ? jsonElement.GetString()
                    : jsonElement.ToString();
            }

            // Deserialize JsonElement to target type
            return JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType, _options);
        }

        // Handle string value
        if (value is string stringValue)
        {
            if (targetType == typeof(string))
                return stringValue;

            // Try to deserialize JSON string
            try
            {
                return JsonSerializer.Deserialize(stringValue, targetType, _options);
            }
            catch
            {
                return null;
            }
        }

        // Try to serialize and deserialize through JSON
        try
        {
            var json = JsonSerializer.Serialize(value, _options);
            return JsonSerializer.Deserialize(json, targetType, _options);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public object? Serialize(object? value)
    {
        if (value == null)
            return null;

        // Primitives and strings can be stored directly
        if (value is string || value.GetType().IsPrimitive)
            return value;

        // Serialize complex objects to JsonElement for consistent storage
        var json = JsonSerializer.Serialize(value, _options);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
