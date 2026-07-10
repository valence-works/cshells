namespace CShells.Lifecycle;

/// <summary>
/// Declares default lifecycle ordering metadata for a lifecycle component (initializer or
/// terminator) type.
/// </summary>
/// <remarks>
/// Explicit metadata supplied by <see cref="ServiceCollectionLifecycleExtensions.AddShellInitializer{TInitializer}(Microsoft.Extensions.DependencyInjection.IServiceCollection, LifecyclePhase, int)"/>
/// or <see cref="ServiceCollectionLifecycleExtensions.AddShellTerminator{TTerminator}(Microsoft.Extensions.DependencyInjection.IServiceCollection, LifecyclePhase, int)"/>
/// takes precedence over this attribute. The attribute is read from the component type and
/// does not require constructing the component before the shell provider exists.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class LifecycleOrderAttribute : Attribute
{
    /// <summary>
    /// Creates ordering metadata in <see cref="LifecyclePhase.Default"/>.
    /// </summary>
    /// <param name="order">The numeric order within <see cref="LifecyclePhase.Default"/>.</param>
    public LifecycleOrderAttribute(int order)
        : this(LifecyclePhase.Default, order)
    {
    }

    /// <summary>
    /// Creates ordering metadata in the specified lifecycle phase.
    /// </summary>
    /// <param name="phase">The semantic lifecycle phase.</param>
    /// <param name="order">The numeric order within <paramref name="phase"/>.</param>
    public LifecycleOrderAttribute(LifecyclePhase phase, int order)
    {
        Phase = phase;
        Order = order;
    }

    /// <summary>The semantic lifecycle phase.</summary>
    public LifecyclePhase Phase { get; }

    /// <summary>The numeric order within <see cref="Phase"/>.</summary>
    public int Order { get; }
}
