namespace CShells.Features;

/// <summary>
/// Well-known keys used when storing feature metadata in <see cref="ShellFeatureDescriptor.Metadata"/>.
/// Centralized so producers (feature discovery) and consumers (the public catalog) stay in sync.
/// </summary>
internal static class ShellFeatureMetadataKeys
{
    /// <summary>
    /// The human-readable display name for a feature, sourced from <c>ShellFeatureAttribute.DisplayName</c>.
    /// </summary>
    public const string DisplayName = "DisplayName";

    /// <summary>
    /// The description of a feature, sourced from <c>ShellFeatureAttribute.Description</c>.
    /// </summary>
    public const string Description = "Description";
}
