using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CShells.AspNetCore;

/// <summary>
/// Extension methods for configuring CShells ASP.NET Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds CShells ASP.NET Core integration services to the service collection.
    /// Registers a default <see cref="IShellResolver"/> that returns a shell with Id "Default"
    /// if no custom resolver has been registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCShellsAspNetCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAddSingleton only adds if no IShellResolver is already registered
        services.TryAddSingleton<IShellResolver, DefaultShellResolver>();

        return services;
    }

    /// <summary>
    /// Default shell resolver that always returns a shell with Id "Default".
    /// </summary>
    private sealed class DefaultShellResolver : IShellResolver
    {
        public ShellId? Resolve(HttpContext httpContext) => new ShellId("Default");
    }
}
