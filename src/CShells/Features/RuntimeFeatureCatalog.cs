using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Features;

internal sealed class RuntimeFeatureCatalog(
    Func<CancellationToken, Task<IReadOnlyCollection<Assembly>>> assemblyResolver,
    ILogger<RuntimeFeatureCatalog>? logger = null) : IRuntimeFeatureCatalog
{
    private readonly Func<CancellationToken, Task<IReadOnlyCollection<Assembly>>> assemblyResolver = Guard.Against.Null(assemblyResolver);
    private readonly ILogger<RuntimeFeatureCatalog> logger = logger ?? NullLogger<RuntimeFeatureCatalog>.Instance;
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    private RuntimeFeatureCatalogSnapshot? currentSnapshot;
    private long nextGeneration;

    public RuntimeFeatureCatalogSnapshot CurrentSnapshot => currentSnapshot
        ?? throw new InvalidOperationException("The runtime feature catalog has not been initialized.");

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (currentSnapshot is not null)
            return;

        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RuntimeFeatureCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return CurrentSnapshot;
    }

    public async Task<RuntimeFeatureCatalogSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var assemblies = await assemblyResolver(cancellationToken).ConfigureAwait(false);
            var descriptors = FeatureDiscovery
                .DiscoverFeatures(
                    assemblies,
                    (assembly, ex) => logger.LogWarning(ex, "Failed to load types from assembly {AssemblyName}. Features in this assembly will not be available.", assembly.GetName().Name))
                .ToList()
                .AsReadOnly();

            var featureMap = descriptors.ToDictionary(descriptor => descriptor.Id, descriptor => descriptor, StringComparer.OrdinalIgnoreCase);
            var snapshot = new RuntimeFeatureCatalogSnapshot(
                Interlocked.Increment(ref nextGeneration),
                assemblies.ToList().AsReadOnly(),
                descriptors,
                featureMap,
                DateTimeOffset.UtcNow);

            currentSnapshot = snapshot;

            logger.LogInformation(
                "Committed runtime feature catalog generation {Generation} with {FeatureCount} feature(s): {FeatureNames}",
                snapshot.Generation,
                snapshot.FeatureDescriptors.Count,
                string.Join(", ", snapshot.FeatureDescriptors.Select(feature => feature.Id)));

            return snapshot;
        }
        finally
        {
            refreshLock.Release();
        }
    }
}

