using System.Text.Json;

namespace CShells.Serialization;

/// <summary>
/// Default implementation of <see cref="IShellPropertySerializer"/> using System.Text.Json.
/// </summary>
public class SystemTextJsonShellPropertySerializer(JsonSerializerOptions? options = null) : IShellPropertySerializer
{
    private readonly JsonSerializerOptions _options = options ?? new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc />
    public T? Deserialize<T>(object? value) => (T?)Deserialize(value, typeof(T));

    /// <inheritdoc />
    public object? Deserialize(object? value, Type targetType)
    {
        Guard.Against.Null(targetType);

        if (value == null)
            return null;

        if (targetType.IsInstanceOfType(value))
            return value;

        if (value is JsonElement jsonElement)
        {
            if (targetType == typeof(string))
                return jsonElement.ValueKind == JsonValueKind.String ? jsonElement.GetString() : jsonElement.ToString();

            return JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType, _options);
        }

        if (value is string stringValue)
        {
            if (targetType == typeof(string))
                return stringValue;

            try
            {
                return JsonSerializer.Deserialize(stringValue, targetType, _options);
            }
            catch
            {
                return null;
            }
        }

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

        if (value is string || value.GetType().IsPrimitive)
            return value;

        var json = JsonSerializer.Serialize(value, _options);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
