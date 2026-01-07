using System.Reflection;
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Middleware;
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
    /// <item>Web routing resolver (path and host-based routing)</item>
    /// <item>Endpoint routing support</item>
    /// <item>Shell resolver orchestrator</item>
    /// </list>
    /// The default resolver strategy can be customized using configuration actions.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action to customize the CShells builder.</param>
    /// <param name="assemblies">Optional assemblies to scan for CShells features. If <c>null</c>, all loaded assemblies are scanned.</param>
    /// <returns>The CShells builder for further configuration.</returns>
    /// <example>
    /// <code>
    /// // Use defaults (web routing + endpoint routing)
    /// services.AddCShellsAspNetCore();
    ///
    /// // Customize resolver pipeline
    /// services.AddCShellsAspNetCore(cshells =>
    /// {
    ///     cshells.WithWebRouting(options =>
    ///     {
    ///         options.HeaderName = "X-Tenant-Id";
    ///         options.ExcludePaths = new[] { "/api", "/admin" };
    ///     });
    /// });
    ///
    /// // Use a custom pipeline
    /// services.AddCShellsAspNetCore(cshells =>
    /// {
    ///     cshells.ConfigureResolverPipeline(pipeline => pipeline
    ///         .Use&lt;CustomResolver&gt;()
    ///         .UseFallback&lt;DefaultShellResolverStrategy&gt;()
    ///     );
    /// });
    /// </code>
    /// </example>
    public static CShellsBuilder AddCShellsAspNetCore(
        this IServiceCollection services,
        Action<CShellsBuilder>? configure = null,
        IEnumerable<Assembly>? assemblies = null)
    {
        Guard.Against.Null(services);

        // Allow user customization first so they can configure the pipeline
        var builder = services.AddCShells(null, assemblies);
        configure?.Invoke(builder);

        // Register shell resolver options for strategy ordering
        services.TryAddSingleton<ShellResolverOptions>();

        // Register the shell resolver orchestrator (only if not already registered)
        services.TryAddSingleton<IShellResolver, DefaultShellResolver>();

        // Register memory cache for shell resolution caching
        services.AddMemoryCache();

        // Register shell middleware options with defaults
        services.AddOptions<ShellMiddlewareOptions>();

        // Apply smart defaults only if pipeline wasn't explicitly configured
        var pipelineWasConfigured = services.Any(d => d.ServiceType == typeof(ResolverPipelineConfigurationMarker));
        if (!pipelineWasConfigured)
        {
            // Default for ASP.NET Core: WebRoutingShellResolver
            builder.WithWebRouting();
        }

        // Register endpoint routing by default
        builder.WithEndpointRouting();

        return builder;
    }
}
