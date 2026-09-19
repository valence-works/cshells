namespace CShells.Features;

/// <summary>
/// An immutable, point-in-time view of the runtime feature catalog. Each refresh produces a new
/// snapshot with an incremented <see cref="Generation"/>.
/// </summary>
public interface IRuntimeFeatureCatalogSnapshot
{
    /// <summary>
    /// Gets the monotonically increasing generation number for this snapshot. A higher value indicates
    /// a more recent catalog state.
    /// </summary>
    long Generation { get; }

    /// <summary>
    /// Gets the UTC timestamp at which this snapshot was produced.
    /// </summary>
    DateTimeOffset RefreshedAt { get; }

    /// <summary>
    /// Gets the descriptors for every feature discovered in this snapshot.
    /// </summary>
    IReadOnlyList<RuntimeFeatureDescriptor> FeatureDescriptors { get; }
}
