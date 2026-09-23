namespace CShells.Features;

/// <summary>
/// The supported, public contract for refreshing and reading the runtime feature catalog.
/// </summary>
/// <remarks>
/// External consumers should resolve this service from the application's root service provider instead
/// of reflecting over CShells internals. It is registered as a singleton by <c>AddCShells</c>.
/// </remarks>
public interface IRuntimeFeatureCatalog
{
    /// <summary>
    /// Returns the detailed discovery snapshot, initializing the catalog if needed.
    /// </summary>
    /// <remarks>
    /// Use this when integrating with feature startup types or custom metadata. Ordinary catalog
    /// display should use <see cref="CurrentSnapshot"/>. Reading does not force a refresh.
    /// Implementations that only support the typed projection may leave this operation unsupported.
    /// </remarks>
    Task<RuntimeFeatureCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This catalog does not expose detailed discovery snapshots.");

    /// <summary>
    /// Gets the most recently committed catalog snapshot.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the catalog has not yet been initialized.</exception>
    IRuntimeFeatureCatalogSnapshot CurrentSnapshot { get; }

    /// <summary>
    /// Ensures the catalog has been initialized at least once, performing an initial refresh if needed.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-evaluates the feature sources and commits a new catalog snapshot.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The newly committed snapshot.</returns>
    Task<IRuntimeFeatureCatalogSnapshot> RefreshAsync(CancellationToken cancellationToken = default);
}
