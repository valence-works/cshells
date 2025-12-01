namespace CShells.Resolution;

/// <summary>
/// Specifies the execution order for a shell resolver strategy.
/// Lower values execute first. If not specified, the default order is 100.
/// </summary>
/// <remarks>
/// <para>
/// This attribute allows resolver strategies to declare their preferred execution order.
/// Common order values:
/// </para>
/// <list type="bullet">
/// <item><description>0-99: High-priority resolvers (e.g., path-based, host-based, claim-based)</description></item>
/// <item><description>100-999: Normal priority custom resolvers (default if not specified)</description></item>
/// <item><description>1000+: Low-priority fallback resolvers (e.g., default shell resolver)</description></item>
/// </list>
/// <para>
/// Users can override the attribute-specified order at registration time using the
/// <c>WithResolverStrategy</c> methods that accept an order parameter.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ResolverOrder(0)]
/// public class PathShellResolver : IShellResolverStrategy
/// {
///     public ShellId? Resolve(ShellResolutionContext context) { ... }
/// }
///
/// [ResolverOrder(1000)]
/// public class DefaultShellResolverStrategy : IShellResolverStrategy
/// {
///     public ShellId? Resolve(ShellResolutionContext context) => new ShellId("Default");
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ResolverOrderAttribute : Attribute
{
    /// <summary>
    /// Gets the execution order for the resolver strategy.
    /// Lower values execute first.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResolverOrderAttribute"/> class.
    /// </summary>
    /// <param name="order">The execution order. Lower values execute first.</param>
    public ResolverOrderAttribute(int order)
    {
        Order = order;
    }
}
