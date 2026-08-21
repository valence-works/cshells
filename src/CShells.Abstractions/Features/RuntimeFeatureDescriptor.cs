namespace CShells.Features;

/// <summary>
/// A stable, public projection of a discovered shell feature, intended for external consumers that
/// need to read the runtime feature catalog without reflecting over CShells internals.
/// </summary>
/// <remarks>
/// This is the supported integration surface for feature metadata. Unlike <see cref="ShellFeatureDescriptor"/>,
/// it exposes <see cref="DisplayName"/> and <see cref="Description"/> as typed members rather than loose
/// metadata entries, and intentionally omits unstable internals such as the feature startup type.
/// </remarks>
public sealed class RuntimeFeatureDescriptor
{
    /// <summary>
    /// Gets the unique identifier for the feature. This is the feature name used throughout CShells.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the feature name. This is an alias for <see cref="Id"/> provided for consumer convenience.
    /// </summary>
    public string Name => Id;

    /// <summary>
    /// Gets the human-readable display name for the feature. Falls back to <see cref="Id"/> when the
    /// feature does not declare an explicit display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the optional description of the feature, or <see langword="null"/> when none was declared.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the names of the features this feature depends on.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];
}
