using Microsoft.Extensions.DependencyInjection;

namespace CShells;

/// <summary>
/// Provides access to the root <see cref="IServiceCollection"/> used during application startup.
/// </summary>
/// <remarks>
/// This accessor allows the shell host to copy root service registrations into each shell's
/// service collection, enabling inheritance of root services while still allowing shell-specific
/// overrides via the "last registration wins" semantics of the DI container.
/// </remarks>
public interface IRootServiceCollectionAccessor
{
    /// <summary>
    /// Gets the root <see cref="IServiceCollection"/> that was used during application startup.
    /// </summary>
    IServiceCollection Services { get; }
}
