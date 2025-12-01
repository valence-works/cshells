using System.Reflection;
using CShells.AspNetCore.Configuration;
using CShells.DependencyInjection;
using CShells.Resolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CShells.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring CShells ASP.NET Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds CShells ASP.NET Core integration services to the service collection with sensible defaults.
    /// By default, this registers:
    /// <list type="bullet">
    /// <item>Standard resolvers (Path and Host)</item>
    /// <item>Endpoint routing support</item>
    /// <item>Shell resolver orchestrator</item>
    /// </list>
    /// Use the optional configure action to customize or opt-out of default registrations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action to customize the CShells builder.</param>
    /// <returns>The CShells builder for further configuration.</returns>
    /// <example>
    /// <code>
    /// // Use defaults (standard resolvers + endpoint routing)
    /// services.AddCShellsAspNetCore();
    ///
    /// // Customize configuration
    /// services.AddCShellsAspNetCore(cshells =>
    /// {
    ///     // Defaults are already registered, but you can add more or customize
    ///     cshells.WithResolverStrategy&lt;ClaimBasedShellResolver&gt;();
    ///     cshells.WithPathResolver(options =>
    ///     {
    ///         options.ExcludePaths = new[] { "/api", "/admin" };
    ///     });
    /// });
    /// </code>
    /// </example>
    public static CShellsBuilder AddCShellsAspNetCore(
        this IServiceCollection services,
        Action<CShellsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register core CShells services first (will scan all loaded assemblies)
        var builder = services.AddCShells();

        // Register shell resolver options for strategy ordering
        services.TryAddSingleton<ShellResolverOptions>();

        // Register the shell resolver orchestrator (only if not already registered)
        services.TryAddSingleton<IShellResolver, DefaultShellResolver>();

        // Register default fallback strategy (only if no strategies are registered)
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy, DefaultShellResolverStrategy>());

        // Register standard resolvers by default (users can opt-out by not calling this in a custom builder)
        builder.WithStandardResolvers();

        // Register endpoint routing by default (users can opt-out in their configure action if needed)
        builder.WithEndpointRouting();

        // Allow user customization
        configure?.Invoke(builder);

        return builder;
    }

    /// <summary>
    /// Adds CShells ASP.NET Core integration services to the service collection with sensible defaults and explicit assembly scanning.
    /// By default, this registers:
    /// <list type="bullet">
    /// <item>Standard resolvers (Path and Host)</item>
    /// <item>Endpoint routing support</item>
    /// <item>Shell resolver orchestrator</item>
    /// </list>
    /// Use the optional configure action to customize or opt-out of default registrations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan for features.</param>
    /// <param name="configure">Optional configuration action to customize the CShells builder.</param>
    /// <returns>The CShells builder for further configuration.</returns>
    public static CShellsBuilder AddCShellsAspNetCore(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        Action<CShellsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        // Register core CShells services first
        var builder = services.AddCShells(assemblies);

        // Register shell resolver options for strategy ordering
        services.TryAddSingleton<ShellResolverOptions>();

        // Register the shell resolver orchestrator (only if not already registered)
        services.TryAddSingleton<IShellResolver, DefaultShellResolver>();

        // Register default fallback strategy (only if no strategies are registered)
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy, DefaultShellResolverStrategy>());

        // Register standard resolvers by default (users can opt-out by not calling this in a custom builder)
        builder.WithStandardResolvers();

        // Register endpoint routing by default (users can opt-out in their configure action if needed)
        builder.WithEndpointRouting();

        // Allow user customization
        configure?.Invoke(builder);

        return builder;
    }
}
