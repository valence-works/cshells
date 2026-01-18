namespace CShells.Hosting;

/// <summary>
/// Default implementation of <see cref="IShellServiceExclusionRegistry"/> that aggregates
/// exclusions from multiple providers.
/// </summary>
public sealed class ShellServiceExclusionRegistry : IShellServiceExclusionRegistry
{
    private readonly Lazy<HashSet<Type>> _excludedTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellServiceExclusionRegistry"/> class.
    /// </summary>
    /// <param name="providers">The exclusion providers to aggregate.</param>
    public ShellServiceExclusionRegistry(IEnumerable<IShellServiceExclusionProvider> providers)
    {
        _excludedTypes = new(() => providers.SelectMany(x => x.GetExcludedServiceTypes()).ToHashSet());
    }

    /// <inheritdoc />
    public IReadOnlySet<Type> ExcludedTypes => _excludedTypes.Value;

    /// <inheritdoc />
    public bool IsExcluded(Type serviceType) => _excludedTypes.Value.Contains(serviceType);
}