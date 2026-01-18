using CShells.DependencyInjection;

namespace CShells.Hosting;

/// <summary>
/// Default implementation of <see cref="IShellServiceExclusionProvider"/> that provides
/// core CShells infrastructure types that should not be copied to shell service collections.
/// </summary>
public class DefaultShellServiceExclusionProvider : IShellServiceExclusionProvider
{
    /// <inheritdoc />
    public IEnumerable<Type> GetExcludedServiceTypes()
    {
        yield return typeof(IShellHost);
        yield return typeof(IShellContextScopeFactory);
        yield return typeof(IRootServiceCollectionAccessor);
        yield return typeof(IReadOnlyCollection<ShellSettings>);
    }
}
