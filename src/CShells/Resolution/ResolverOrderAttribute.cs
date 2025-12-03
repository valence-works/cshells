namespace CShells.Resolution;

/// <summary>
/// Specifies the execution order for a shell resolver strategy.
/// Lower values execute first. If not specified, the default order is 100.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ResolverOrderAttribute(int order) : Attribute
{
    /// <summary>
    /// Gets the execution order for the resolver strategy.
    /// </summary>
    public int Order { get; } = order;
}
