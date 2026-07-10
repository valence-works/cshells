namespace CShells.Lifecycle;

/// <summary>
/// Ordering metadata for an <see cref="IShellTerminator"/> registration.
/// </summary>
/// <param name="TerminatorType">Concrete terminator implementation type.</param>
/// <param name="Phase">Semantic lifecycle phase.</param>
/// <param name="Order">Numeric order within <paramref name="Phase"/>.</param>
/// <param name="RegistrationIndex">
/// Zero-based ordinal of the associated <see cref="IShellTerminator"/> service descriptor, or
/// <c>-1</c> when the metadata should be matched by terminator type.
/// </param>
/// <param name="IsExplicit">Whether this metadata came from an explicitly ordered lifecycle API.</param>
/// <param name="Source">Human-readable metadata source for diagnostics.</param>
/// <remarks>
/// <see cref="ServiceCollectionLifecycleExtensions.AddShellTerminator{TTerminator}(Microsoft.Extensions.DependencyInjection.IServiceCollection, LifecyclePhase, int)"/>
/// registers terminator implementations with transient lifetime. Existing unordered
/// <see cref="IShellTerminator"/> registrations do not need this metadata and are treated as
/// <see cref="LifecyclePhase.Default"/> entries.
/// </remarks>
public sealed record ShellTerminatorRegistration(
    Type TerminatorType,
    LifecyclePhase Phase,
    int Order,
    int RegistrationIndex,
    bool IsExplicit,
    string Source);
