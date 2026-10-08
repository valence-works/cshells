using System.Reflection;
using CShells.Features;
using Nuplane.Loading;

namespace CShells.Nuplane;

/// <summary>
/// Contributes assemblies Nuplane has already loaded for active packages to CShells feature discovery.
/// </summary>
/// <remarks>
/// The provider returns loaded <see cref="Assembly"/> instances in package-catalog order. It does not load
/// package files or use <see cref="PackageAssemblies.AssemblyReferences"/>.
/// </remarks>
public sealed class NuplaneFeatureAssemblyProvider(IPackageAssemblyCatalog packageAssemblyCatalog) : IFeatureAssemblyProvider
{
    private readonly IPackageAssemblyCatalog packageCatalog = packageAssemblyCatalog
        ?? throw new ArgumentNullException(nameof(packageAssemblyCatalog));

    /// <inheritdoc />
    public async Task<IEnumerable<Assembly>> GetAssembliesAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        cancellationToken.ThrowIfCancellationRequested();

        var packages = await packageCatalog.GetPackagedAssembliesAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var assemblies = new List<Assembly>();
        foreach (var package in packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var assembly in package.Assemblies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                assemblies.Add(assembly);
            }
        }

        return assemblies;
    }
}
