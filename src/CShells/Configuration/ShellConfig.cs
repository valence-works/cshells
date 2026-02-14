using System.Text.Json.Serialization;

namespace CShells.Configuration;

/// <summary>
/// Configuration model for a shell section in appsettings.json.
/// </summary>
public class ShellConfig
{
    /// <summary>
    /// Gets or sets the name of the shell.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of enabled features for this shell.
    /// Each entry can be a simple string (feature name) or an object with Name and settings.
    /// </summary>
    /// <example>
    /// <code>
    /// "Features": [
    ///   "Core",
    ///   { "Name": "FraudDetection", "Threshold": 0.85 },
    ///   "EmailNotification"
    /// ]
    /// </code>
    /// </example>
    [JsonConverter(typeof(FeatureEntryListJsonConverter))]
    public List<FeatureEntry> Features { get; set; } = [];

    /// <summary>
    /// Gets or sets shell-specific configuration.
    /// These settings are available via IConfiguration in the shell's service provider.
    /// </summary>
    public Dictionary<string, object?> Configuration { get; set; } = new();
}
