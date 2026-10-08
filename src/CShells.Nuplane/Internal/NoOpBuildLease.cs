using CShells.Features;
using CShells.Lifecycle;

namespace CShells.Nuplane.Internal;

internal sealed class NoOpBuildLease : IShellGenerationBuildLease
{
    public static NoOpBuildLease Instance { get; } = new();

    private NoOpBuildLease()
    {
    }

    public ValueTask OnSnapshotSelectedAsync(
        RuntimeFeatureCatalogSnapshot snapshot,
        CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
