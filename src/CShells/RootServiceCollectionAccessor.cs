using Microsoft.Extensions.DependencyInjection;

namespace CShells;

/// <summary>
/// Default implementation of <see cref="IRootServiceCollectionAccessor"/> that stores
/// a reference to the root <see cref="IServiceCollection"/>.
/// </summary>
internal sealed class RootServiceCollectionAccessor : IRootServiceCollectionAccessor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RootServiceCollectionAccessor"/> class.
    /// </summary>
    /// <param name="services">The root <see cref="IServiceCollection"/> to provide access to.</param>
    public RootServiceCollectionAccessor(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc />
    public IServiceCollection Services { get; }
}
