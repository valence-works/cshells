namespace CShells.Features;

/// <summary>
/// An attribute that defines a feature's metadata for shell startup classes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ShellFeatureAttribute(string? name = null) : Attribute
{
    /// <summary>
    /// Gets the name of the feature. If null, the name will be derived from the class name.
    /// </summary>
    public string? Name { get; } = name;

    /// <summary>
    /// Gets or sets the display name for this feature. If not set, the <see cref="Name"/> is used.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description of this feature.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the feature names that this feature depends on.
    /// </summary>
    public string[] DependsOn { get; set; } = [];

    /// <summary>
    /// Gets or sets the metadata associated with this feature.
    /// </summary>
    public object[] Metadata { get; set; } = [];
}
