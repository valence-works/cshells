namespace CShells.Features;

/// <summary>
/// Adapts the internal <see cref="RuntimeFeatureCatalog"/> to the public <see cref="IRuntimeFeatureCatalog"/>
/// contract, exposing only the stable members external consumers need.
/// </summary>
internal sealed class RuntimeFeatureCatalogAccessor(RuntimeFeatureCatalog catalog) : IRuntimeFeatureCatalog
{
    private readonly RuntimeFeatureCatalog catalog = Guard.Against.Null(catalog);

    /// <inheritdoc />
    public IRuntimeFeatureCatalogSnapshot CurrentSnapshot => Map(catalog.CurrentSnapshot);

    /// <inheritdoc />
    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) =>
        catalog.EnsureInitializedAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IRuntimeFeatureCatalogSnapshot> RefreshAsync(CancellationToken cancellationToken = default) =>
        Map(await catalog.RefreshAsync(cancellationToken).ConfigureAwait(false));

    private static IRuntimeFeatureCatalogSnapshot Map(RuntimeFeatureCatalogSnapshot snapshot)
    {
        var descriptors = snapshot.FeatureDescriptors.Select(MapDescriptor).ToList().AsReadOnly();
        return new RuntimeFeatureCatalogSnapshotView(snapshot.Generation, snapshot.RefreshedAt, descriptors);
    }

    private static RuntimeFeatureDescriptor MapDescriptor(ShellFeatureDescriptor descriptor)
    {
        var displayName = GetMetadataString(descriptor, "DisplayName") ?? descriptor.Id;
        var description = GetMetadataString(descriptor, "Description");

        return new RuntimeFeatureDescriptor
        {
            Id = descriptor.Id,
            DisplayName = displayName,
            Description = description,
            Dependencies = descriptor.Dependencies,
        };
    }

    private static string? GetMetadataString(ShellFeatureDescriptor descriptor, string key) =>
        descriptor.Metadata.TryGetValue(key, out var value) ? value as string : null;

    private sealed record RuntimeFeatureCatalogSnapshotView(
        long Generation,
        DateTimeOffset RefreshedAt,
        IReadOnlyList<RuntimeFeatureDescriptor> FeatureDescriptors) : IRuntimeFeatureCatalogSnapshot;
}
