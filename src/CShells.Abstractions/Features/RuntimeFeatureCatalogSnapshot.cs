using System.Reflection;

namespace CShells.Features;

/// <summary>
/// An immutable snapshot of the shell features discovered across every configured feature assembly provider
/// (explicit assemblies, host assemblies, and custom <see cref="IFeatureAssemblyProvider"/>s) at a point in time.
/// </summary>
/// <param name="Generation">A monotonically increasing number identifying this snapshot; a later snapshot always has a greater generation.</param>
/// <param name="Assemblies">The assemblies that were scanned to produce this snapshot.</param>
/// <param name="FeatureDescriptors">All discovered feature descriptors.</param>
/// <param name="FeatureMap">The discovered features keyed by <see cref="ShellFeatureDescriptor.Id"/> (case-insensitive).</param>
/// <param name="RefreshedAt">The UTC time the snapshot was committed.</param>
public sealed record RuntimeFeatureCatalogSnapshot(
    long Generation,
    IReadOnlyCollection<Assembly> Assemblies,
    IReadOnlyCollection<ShellFeatureDescriptor> FeatureDescriptors,
    IReadOnlyDictionary<string, ShellFeatureDescriptor> FeatureMap,
    DateTimeOffset RefreshedAt);
