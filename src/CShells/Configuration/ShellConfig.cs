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
    /// Gets or sets arbitrary properties associated with this shell.
    /// Note: Configuration binding creates these as objects. They will be converted
    /// to JsonElement during ShellSettings creation for proper serialization support.
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = new();
}
