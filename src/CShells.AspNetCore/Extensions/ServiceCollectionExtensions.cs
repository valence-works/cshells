using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Middleware;
using CShells.AspNetCore.Routing;
using CShells.DependencyInjection;
using CShells.Hosting;
using CShells.Lifecycle;
using CShells.Resolution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CShells.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring CShells ASP.NET Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds CShells ASP.NET Core integration services. Registers sensible defaults:
    /// web routing resolver (path + host-based), endpoint routing, the shell resolver
    /// orchestrator, the blueprint-backed route index, and HTTP context accessor.
    /// </summary>
    public static CShellsBuilder AddCShellsAspNetCore(
        this IServiceCollection services,
        Action<CShellsBuilder>? configure = null)
    {
        Guard.Against.Null(services);

        var builder = services.AddCShells(configure);

        services.TryAddSingleton<ShellResolverOptions>();
        services.TryAddSingleton<IShellResolver, DefaultShellResolver>();

        // Guarantees IMiddleware-style middleware activation inside shell containers even when the
        // root host never registered a factory (plain ServiceCollection / generic host). Scoped to
        // match the framework's own registration in GenericWebHostBuilder; TryAdd defers to any host
        // registration, and the descriptor flows into shell containers via
        // ShellProviderBuilder.CopyRootServices.
        services.TryAddScoped<IMiddlewareFactory, MiddlewareFactory>();
        services.AddMemoryCache();
        services.AddOptions<ShellMiddlewareOptions>();

        // Route index (feature 010): blueprint-aware routing without requiring shells to be
        // pre-warmed or already active. The invalidator hooks the registry's lifecycle
        // notification fan-out so the snapshot is refreshed when blueprints come and go.
        services.TryAddSingleton<IShellRouteIndex>(sp => new DefaultShellRouteIndex(
            sp.GetRequiredService<IShellBlueprintProvider>(),
            sp.GetService<Microsoft.Extensions.Logging.ILogger<DefaultShellRouteIndex>>()));
        services.AddSingleton<ShellRouteIndexInvalidator>();
        services.AddSingleton<IHostedService, ShellRouteIndexInvalidatorHostedService>();

        var pipelineWasConfigured = services.Any(d => d.ServiceType == typeof(ResolverPipelineConfigurationMarker));
        if (!pipelineWasConfigured)
        {
            services.TryAddSingleton(new Resolution.WebRoutingShellResolverOptions());

            services.AddSingleton<IShellResolverStrategy>(sp => new Resolution.WebRoutingShellResolver(
                sp.GetRequiredService<IShellRouteIndex>(),
                sp.GetRequiredService<Resolution.WebRoutingShellResolverOptions>(),
                sp.GetService<Microsoft.Extensions.Logging.ILogger<Resolution.WebRoutingShellResolver>>()));
            services.AddSingleton<IShellResolverStrategy, DefaultShellResolverStrategy>();

            services.Configure<ShellResolverOptions>(opt =>
            {
                opt.SetOrder<Resolution.WebRoutingShellResolver>(0);
                opt.SetOrder<DefaultShellResolverStrategy>(1000);
            });
        }

        builder.WithEndpointRouting();

        services.AddSingleton<IShellServiceExclusionProvider, Hosting.AspNetCoreShellServiceExclusionProvider>();
        services.AddHttpContextAccessor();

        return builder;
    }
}
