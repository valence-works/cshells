namespace CShells.Hosting;

/// <summary>
/// A registry that aggregates service types from multiple providers that should NOT be copied
/// from the root service collection into shell service collections.
/// </summary>
public interface IShellServiceExclusionRegistry
{
    /// <summary>
    /// Gets the aggregated set of excluded service types from all registered providers.
    /// </summary>
    IReadOnlySet<Type> ExcludedTypes { get; }

    /// <summary>
    /// Checks if a service type is excluded.
    /// </summary>
    /// <param name="serviceType">The service type to check.</param>
    /// <returns>True if the type is excluded; otherwise false.</returns>
    bool IsExcluded(Type serviceType);
}
