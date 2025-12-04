namespace CShells.Features;

/// <summary>
/// An attribute that defines a feature's metadata for shell startup classes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ShellFeatureAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the name of the feature.
    /// </summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// Gets or sets the display name for this feature. If not set, the <see cref="Name"/> is used.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the feature names that this feature depends on.
    /// </summary>
    public string[] DependsOn { get; set; } = [];

    /// <summary>
    /// Gets or sets the metadata associated with this feature.
    /// </summary>
    public object[] Metadata { get; set; } = [];
}
