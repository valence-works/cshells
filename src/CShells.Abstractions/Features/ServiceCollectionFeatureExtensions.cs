using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CShells.Features;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to simplify feature options registration.
/// </summary>
public static class ServiceCollectionFeatureExtensions
{
    /// <summary>
    /// Registers a pre-configured options snapshot as <see cref="IOptions{TOptions}"/> in the service collection.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="options">
    /// The already-bound options instance to register. Typically the value received via
    /// <see cref="IConfigurableFeature{TOptions}.Configure"/> before <c>ConfigureServices</c> runs.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This is the recommended way for <see cref="IConfigurableFeature{TOptions}"/> implementations
    /// to expose their bound configuration as <see cref="IOptions{TOptions}"/> without
    /// manually copying every property.
    /// </para>
    /// <para>
    /// The options are registered as a <b>singleton</b> snapshot, which is the correct semantic for
    /// shell features: configuration is fixed at shell build time and does not change at runtime.
    /// </para>
    /// <para>
    /// Example usage inside <c>ConfigureServices</c>:
    /// </para>
    /// <code>
    /// public class IdentityFeature : IFastEndpointsShellFeature, IConfigurableFeature&lt;IdentityTokenOptions&gt;
    /// {
    ///     private IdentityTokenOptions _tokenOptions = new();
    ///
    ///     public void Configure(IdentityTokenOptions options) => _tokenOptions = options;
    ///
    ///     public void ConfigureServices(IServiceCollection services)
    ///     {
    ///         services.RegisterFeatureOptions(_tokenOptions);
    ///         // ... rest of registration
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection RegisterFeatureOptions<TOptions>(
        this IServiceCollection services,
        TOptions options)
        where TOptions : class
    {
        Guard.Against.Null(services);
        Guard.Against.Null(options);

        services.AddSingleton(Options.Create(options));
        return services;
    }
}


