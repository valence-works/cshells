using System.Collections.ObjectModel;

namespace CShells.Lifecycle;

/// <summary>
/// Configuration patch returned by <see cref="IShellSettingsPreparer"/>.
/// </summary>
/// <remarks>
/// Entries with a non-null value set or replace one scalar configuration value. Entries with a null
/// value remove that key. Unrelated typed values in <see cref="ShellSettings.ConfigurationData"/>
/// retain their existing value and identity.
/// </remarks>
public sealed class ShellSettingsPreparationResult
{
    /// <summary>Initializes a result with scalar configuration changes.</summary>
    /// <param name="configurationData">Keys to set or remove; null values remove keys.</param>
    public ShellSettingsPreparationResult(IReadOnlyDictionary<string, string?> configurationData)
    {
        Guard.Against.Null(configurationData);
        ConfigurationData = new ReadOnlyDictionary<string, string?>(
            configurationData.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Gets the immutable scalar configuration patch.</summary>
    public IReadOnlyDictionary<string, string?> ConfigurationData { get; }

    /// <summary>Returns an empty patch that preserves the composed shell settings.</summary>
    /// <param name="context">The preparation context to preserve.</param>
    /// <returns>An empty configuration patch.</returns>
    public static ShellSettingsPreparationResult Unchanged(ShellSettingsPreparationContext context)
    {
        Guard.Against.Null(context);
        return new ShellSettingsPreparationResult(new Dictionary<string, string?>());
    }
}
