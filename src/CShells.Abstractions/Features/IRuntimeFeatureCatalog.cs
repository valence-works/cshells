namespace CShells.Features;

/// <summary>
/// Provides read access to the set of shell features discovered across every configured feature assembly
/// provider (explicit assemblies, host assemblies, and custom <see cref="IFeatureAssemblyProvider"/>s).
/// </summary>
/// <remarks>
/// This is the authoritative catalog of features the host <em>can</em> activate. Whether any given feature is
/// actually enabled is decided per shell by its configuration, independently of this catalog — so a host can use
/// this to present "available" features (enabled or not) without re-implementing feature discovery.
/// </remarks>
public interface IRuntimeFeatureCatalog
{
    /// <summary>
    /// Returns the current catalog snapshot, performing a first discovery pass if the catalog has not been
    /// initialized yet.
    /// </summary>
    Task<RuntimeFeatureCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-discovers features from all configured providers and commits (and returns) a new snapshot.
    /// </summary>
    Task<RuntimeFeatureCatalogSnapshot> RefreshAsync(CancellationToken cancellationToken = default);
}
