using CShells.Serialization;

namespace CShells;

/// <summary>
/// Extension methods for <see cref="ShellSettings"/> to simplify property access.
/// </summary>
public static class ShellSettingsExtensions
{
    private static IShellPropertySerializer? _defaultSerializer;

    /// <summary>
    /// Gets or sets the default property serializer used when no serializer is explicitly provided.
    /// Defaults to <see cref="SystemTextJsonShellPropertySerializer"/>.
    /// </summary>
    public static IShellPropertySerializer DefaultSerializer
    {
        get => _defaultSerializer ??= new SystemTextJsonShellPropertySerializer();
        set => _defaultSerializer = Guard.Against.Null(value);
    }

    /// <summary>
    /// Gets a property value from the shell settings and converts it to the specified type.
    /// </summary>
    /// <typeparam name="T">The target type to convert the property value to.</typeparam>
    /// <param name="settings">The shell settings.</param>
    /// <param name="key">The property key.</param>
    /// <param name="serializer">Optional custom serializer. If null, uses the default serializer.</param>
    /// <returns>The property value converted to the specified type, or default(T) if the property doesn't exist or conversion fails.</returns>
    public static T? GetProperty<T>(this ShellSettings settings, string key, IShellPropertySerializer? serializer = null)
    {
        Guard.Against.Null(settings);
        Guard.Against.NullOrWhiteSpace(key);

        if (!settings.Properties.TryGetValue(key, out var value))
            return default;

        var actualSerializer = serializer ?? DefaultSerializer;
        return actualSerializer.Deserialize<T>(value);
    }

    /// <summary>
    /// Gets a property value from the shell settings and converts it to the specified type.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    /// <param name="key">The property key.</param>
    /// <param name="targetType">The target type to convert the property value to.</param>
    /// <param name="serializer">Optional custom serializer. If null, uses the default serializer.</param>
    /// <returns>The property value converted to the specified type, or null if the property doesn't exist or conversion fails.</returns>
    public static object? GetProperty(this ShellSettings settings, string key, Type targetType, IShellPropertySerializer? serializer = null)
    {
        Guard.Against.Null(settings);
        Guard.Against.NullOrWhiteSpace(key);
        Guard.Against.Null(targetType);

        if (!settings.Properties.TryGetValue(key, out var value))
            return null;

        var actualSerializer = serializer ?? DefaultSerializer;
        return actualSerializer.Deserialize(value, targetType);
    }

    /// <summary>
    /// Sets a property value in the shell settings, serializing it if necessary.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="settings">The shell settings.</param>
    /// <param name="key">The property key.</param>
    /// <param name="value">The property value.</param>
    /// <param name="serializer">Optional custom serializer. If null, uses the default serializer.</param>
    public static void SetProperty<T>(this ShellSettings settings, string key, T value, IShellPropertySerializer? serializer = null)
    {
        Guard.Against.Null(settings);
        Guard.Against.NullOrWhiteSpace(key);

        var actualSerializer = serializer ?? DefaultSerializer;
        settings.Properties[key] = actualSerializer.Serialize(value) ?? value!;
    }

    /// <summary>
    /// Tries to get a property value from the shell settings and converts it to the specified type.
    /// </summary>
    /// <typeparam name="T">The target type to convert the property value to.</typeparam>
    /// <param name="settings">The shell settings.</param>
    /// <param name="key">The property key.</param>
    /// <param name="value">When this method returns, contains the property value if found and converted successfully; otherwise, default(T).</param>
    /// <param name="serializer">Optional custom serializer. If null, uses the default serializer.</param>
    /// <returns>true if the property was found and converted successfully; otherwise, false.</returns>
    public static bool TryGetProperty<T>(this ShellSettings settings, string key, out T? value, IShellPropertySerializer? serializer = null)
    {
        Guard.Against.Null(settings);
        Guard.Against.NullOrWhiteSpace(key);

        if (!settings.Properties.TryGetValue(key, out var rawValue))
        {
            value = default;
            return false;
        }

        var actualSerializer = serializer ?? DefaultSerializer;
        value = actualSerializer.Deserialize<T>(rawValue);
        return value != null || rawValue == null;
    }
}
