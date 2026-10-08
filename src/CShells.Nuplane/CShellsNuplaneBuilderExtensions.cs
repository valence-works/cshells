using CShells.DependencyInjection;
using CShells.Lifecycle;
using CShells.Nuplane.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nuplane.Abstractions;

namespace CShells.Nuplane;

/// <summary>Composition extensions for the optional Nuplane feature-discovery adapter.</summary>
public static class CShellsNuplaneBuilderExtensions
{
    /// <summary>
    /// Selects Nuplane's loaded package assemblies as a CShells feature source and registers deferred catalog
    /// freshness coordination.
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <param name="configure">Optional configuration for refresh and reload behavior.</param>
    /// <returns>The same builder for fluent composition.</returns>
    /// <remarks>
    /// Call this after configuring Nuplane autoload and any existing Nuplane observers. This appends the adapter's
    /// observer after those service registrations. The shared shell-provider bridge is private and contains only the
    /// root-owned coordinator; unrelated observer registrations retain their ordinary DI behavior.
    /// </remarks>
    public static CShellsBuilder WithNuplaneFeatureDiscovery(
        this CShellsBuilder builder,
        Action<NuplaneIntegrationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        if (configure is not null)
            services.Configure(configure);
        else
            services.AddOptions<NuplaneIntegrationOptions>();

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(CoordinatorHolder)))
        {
            services.TryAddSingleton<NuplaneFeatureAssemblyProvider>();
            builder.WithAssemblyProvider<NuplaneFeatureAssemblyProvider>();
            builder.ShareSingletonWithShells<CoordinatorHolder>();

            services.AddSingleton<CoordinatorHolder>(serviceProvider => new CoordinatorHolder(
                new NuplaneRefreshCoordinator(
                    serviceProvider.GetRequiredService<CShells.Features.IRuntimeFeatureCatalog>(),
                    serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<NuplaneIntegrationOptions>>(),
                    () => serviceProvider.GetRequiredService<IShellRegistry>())));
            services.AddSingleton<INuplaneObserver>(serviceProvider => serviceProvider.GetRequiredService<CoordinatorHolder>().Coordinator);
            services.AddSingleton<IShellGenerationBuildParticipant>(serviceProvider => serviceProvider.GetRequiredService<CoordinatorHolder>().Coordinator);
        }

        return builder;
    }
}
