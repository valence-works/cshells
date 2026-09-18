namespace CShells.Lifecycle;

/// <summary>
/// Ordering metadata for an <see cref="IShellInitializer"/> registration.
/// </summary>
/// <param name="InitializerType">Concrete initializer implementation type.</param>
/// <param name="Phase">Semantic lifecycle phase.</param>
/// <param name="Order">Numeric order within <paramref name="Phase"/>.</param>
/// <param name="RegistrationIndex">
/// Zero-based ordinal of the associated <see cref="IShellInitializer"/> service descriptor, or
/// <c>-1</c> when the metadata should be matched by initializer type.
/// </param>
/// <param name="IsExplicit">Whether this metadata came from an explicitly ordered lifecycle API.</param>
/// <param name="Source">Human-readable metadata source for diagnostics.</param>
/// <remarks>
/// <see cref="ServiceCollectionLifecycleExtensions.AddShellInitializer{TInitializer}(Microsoft.Extensions.DependencyInjection.IServiceCollection, LifecyclePhase, int)"/>
/// registers initializer implementations with transient lifetime unless the consumer has
/// already registered the implementation type, in which case that lifetime wins. Existing unordered
/// <see cref="IShellInitializer"/> registrations do not need this metadata and are treated as
/// <see cref="LifecyclePhase.Default"/> entries.
/// </remarks>
public sealed record ShellInitializerRegistration(
    Type InitializerType,
    LifecyclePhase Phase,
    int Order,
    int RegistrationIndex,
    bool IsExplicit,
    string Source);
