using CShells.Features;

namespace CShells.Lifecycle;

/// <summary>
/// Holds participant-owned protection for one shell-generation build and its service-provider lifetime.
/// </summary>
/// <remarks>
/// CShells calls <see cref="OnSnapshotSelectedAsync"/> with the exact detailed snapshot used to select features,
/// before feature construction. It calls <see cref="IAsyncDisposable.DisposeAsync"/> after complete provider
/// teardown for a published generation, or while unwinding a failed build or unpublished initializer candidate.
/// Published generations also require a successful Disposed lifecycle notification before release. If release throws, the
/// lease must preserve any external protection that it cannot prove has been released; CShells retains the
/// unresolved lease object for the root host lifetime and does not automatically retry the release.
/// </remarks>
public interface IShellGenerationBuildLease : IAsyncDisposable
{
    /// <summary>
    /// Associates this lease with the exact feature-catalog snapshot selected for the shell build.
    /// </summary>
    /// <param name="snapshot">The immutable detailed snapshot used by feature selection.</param>
    /// <param name="cancellationToken">A token that can cancel the build attempt.</param>
    /// <returns>A task that completes when this lease has incorporated the snapshot identity.</returns>
    /// <remarks>Each acquired lease receives one callback in registration order during successful fan-out. If an earlier lease callback fails or cancels the build, this callback may not be reached.</remarks>
    ValueTask OnSnapshotSelectedAsync(
        RuntimeFeatureCatalogSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
