namespace CShells.Configuration;

/// <summary>
/// Represents a feature entry in shell configuration.
/// Can be created from either a simple string (feature name only) or 
/// an object with feature name and settings.
/// </summary>
public class FeatureEntry
{
    /// <summary>
    /// Gets or sets the feature name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the feature-specific settings.
    /// All properties other than 'Name' are treated as settings.
    /// </summary>
    public Dictionary<string, object?> Settings { get; set; } = new();

    /// <summary>
    /// Creates a feature entry from just a feature name (no settings).
    /// </summary>
    /// <param name="name">The feature name.</param>
    /// <returns>A new <see cref="FeatureEntry"/> with only the name set.</returns>
    public static FeatureEntry FromName(string name) => new() { Name = name };

    /// <summary>
    /// Returns the feature name.
    /// </summary>
    public override string ToString() => Name;
}

