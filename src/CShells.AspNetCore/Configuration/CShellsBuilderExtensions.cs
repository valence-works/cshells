using CShells.Configuration;
using CShells.DependencyInjection;
using CShells.AspNetCore.Resolution;
using CShells.Resolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CShells.AspNetCore.Configuration;

/// <summary>
/// Extension methods for <see cref="CShellsBuilder"/> to configure ASP.NET Core-specific shell resolution.
/// </summary>
public static class CShellsBuilderExtensions
{
    /// <summary>
    /// Automatically registers shell resolution strategies that read shell properties at runtime.
    /// Resolvers query Path and Host properties from the shell settings cache.
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <remarks>
    /// This method registers:
    /// <list type="bullet">
    /// <item><see cref="IShellSettingsCache"/> as a hosted service that loads shells at startup</item>
    /// <item><see cref="PathShellResolver"/> to resolve shells by URL path segment</item>
    /// <item><see cref="HostShellResolver"/> to resolve shells by HTTP host name</item>
    /// </list>
    /// The resolvers query shell properties at runtime from the cache, enabling dynamic shell configuration.
    /// </remarks>
    public static CShellsBuilder WithAutoResolvers(this CShellsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register the shell settings cache as a hosted service
        // This loads shells at startup and keeps them cached for runtime resolution
        builder.Services.TryAddSingleton<IShellSettingsCache, DefaultShellSettingsCache>();
        builder.Services.AddHostedService(sp => (DefaultShellSettingsCache)sp.GetRequiredService<IShellSettingsCache>());

        // Register resolvers that read from the cache at runtime
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy, PathShellResolver>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy, HostShellResolver>());

        return builder;
    }
}
