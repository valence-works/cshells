namespace CShells;

/// <summary>
/// Extension methods for <see cref="ShellSettings"/> to simplify configuration access.
/// </summary>
public static class ShellSettingsExtensions
{
    /// <summary>
    /// Gets a configuration value from the shell settings.
    /// </summary>
    /// <typeparam name="T">The target type to convert the value to.</typeparam>
    /// <param name="settings">The shell settings.</param>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration value converted to the specified type, or default(T) if not found.</returns>
    public static T? GetConfiguration<T>(this ShellSettings settings, string key)
    {
        Guard.Against.Null(settings);
        Guard.Against.NullOrWhiteSpace(key);

        if (!settings.ConfigurationData.TryGetValue(key, out var value))
            return default;

        if (value is T typedValue)
            return typedValue;

        // Try to convert string values
        if (value is string stringValue && typeof(T) != typeof(string))
        {
            try
            {
                return (T)Convert.ChangeType(stringValue, typeof(T));
            }
            catch
            {
                return default;
            }
        }

        return default;
    }

    /// <summary>
    /// Gets a configuration value from the shell settings as a string.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration value as string, or null if not found.</returns>
    public static string? GetConfiguration(this ShellSettings settings, string key)
    {
        Guard.Against.Null(settings);
        Guard.Against.NullOrWhiteSpace(key);

        if (!settings.ConfigurationData.TryGetValue(key, out var value))
            return null;

        return value?.ToString();
    }

    /// <summary>
    /// Tries to get a configuration value from the shell settings.
    /// </summary>
    /// <typeparam name="T">The target type to convert the value to.</typeparam>
    /// <param name="settings">The shell settings.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">When this method returns, contains the value if found; otherwise, default(T).</param>
    /// <returns>true if the configuration was found; otherwise, false.</returns>
    public static bool TryGetConfiguration<T>(this ShellSettings settings, string key, out T? value)
    {
        Guard.Against.Null(settings);
        Guard.Against.NullOrWhiteSpace(key);

        if (!settings.ConfigurationData.TryGetValue(key, out var rawValue))
        {
            value = default;
            return false;
        }

        if (rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        // Try to convert string values
        if (rawValue is string stringValue && typeof(T) != typeof(string))
        {
            try
            {
                value = (T)Convert.ChangeType(stringValue, typeof(T));
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Sets a configuration value in the shell settings.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The configuration value.</param>
    public static void SetConfiguration(this ShellSettings settings, string key, object value)
    {
        Guard.Against.Null(settings);
        Guard.Against.NullOrWhiteSpace(key);
        Guard.Against.Null(value);

        settings.ConfigurationData[key] = value;
    }
}
