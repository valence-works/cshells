namespace CShells.Hosting;

/// <summary>
/// Provides a set of service types that should NOT be copied from the root service collection
/// into shell service collections.
/// </summary>
/// <remarks>
/// <para>
/// This allows different packages (like CShells.AspNetCore) to declare types that should
/// remain exclusive to either the root or shell contexts without creating coupling between
/// the core CShells package and framework-specific packages.
/// </para>
/// <para>
/// Implementations should be registered as services, and the <see cref="IShellServiceExclusionRegistry"/>
/// will aggregate all providers to build the complete exclusion list.
/// </para>
/// </remarks>
public interface IShellServiceExclusionProvider
{
    /// <summary>
    /// Gets the collection of service types that should be excluded from shell service collections.
    /// </summary>
    IEnumerable<Type> GetExcludedServiceTypes();
}
