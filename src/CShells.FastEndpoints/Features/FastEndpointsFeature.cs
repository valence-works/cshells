using System.Reflection;
using CShells.AspNetCore.Features;
using CShells.FastEndpoints.Contracts;
using CShells.FastEndpoints.Options;
using CShells.Features;
using FastEndpoints;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CShells.FastEndpoints.Features;

/// <summary>
/// Provides FastEndpoints integration for shell features.
/// Discovers all features implementing <see cref="IFastEndpointsShellFeature"/> and registers their endpoints.
/// </summary>
[ShellFeature]
[UsedImplicitly]
public class FastEndpointsFeature(
    ShellFeatureContext context,
    ILogger<FastEndpointsFeature>? logger = null) : IWebShellFeature
{
    private readonly ShellSettings _shellSettings = context.Settings;
    private readonly IReadOnlyCollection<ShellFeatureDescriptor> _allFeatureDescriptors = context.AllFeatures;
    private readonly ILogger<FastEndpointsFeature> _logger = logger ?? NullLogger<FastEndpointsFeature>.Instance;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Discover all FastEndpoints assemblies from enabled features
        var assemblies = DiscoverFastEndpointsAssemblies();

        _logger.LogInformation("Configuring FastEndpoints for shell '{ShellId}' with {AssemblyCount} assembly(ies)",
            _shellSettings.Id, assemblies.Count);

        services
            .AddOptions<FastEndpointsOptions>()
            .BindConfiguration("FastEndpoints");

        // Register FastEndpoints with the discovered assemblies
        services.AddFastEndpoints(options =>
        {
            options.DisableAutoDiscovery = true;
            options.Assemblies = assemblies;
        });
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        _logger.LogInformation("Mapping FastEndpoints for shell '{ShellId}'", _shellSettings.Id);
        
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<FastEndpointsOptions>>().Value;
        
        // Map FastEndpoints - the ShellEndpointRouteBuilder will automatically apply both
        // the shell's path prefix and the WebRouting:RoutePrefix (if configured)
        endpoints.MapFastEndpoints(config =>
        {
            // Apply FastEndpoints-specific endpoint route prefix if configured
            if (!string.IsNullOrWhiteSpace(options.EndpointRoutePrefix))
            {
                config.Endpoints.RoutePrefix = options.EndpointRoutePrefix;
                _logger.LogInformation("Applied FastEndpoints route prefix '{Prefix}' for shell '{ShellId}'", options.EndpointRoutePrefix, _shellSettings.Id);
            }

            // Discover and invoke all registered configurators
            var serviceProvider = endpoints.ServiceProvider;
            var configurators = serviceProvider.GetServices<IFastEndpointsConfigurator>();

            foreach (var configurator in configurators)
            {
                _logger.LogInformation("Applying FastEndpoints configurator '{ConfiguratorType}' for shell '{ShellId}'",
                    configurator.GetType().Name, _shellSettings.Id);
                configurator.Configure(config);
            }
        });
    }

    /// <summary>
    /// Discovers all assemblies containing FastEndpoints from features implementing <see cref="IFastEndpointsShellFeature"/>.
    /// </summary>
    /// <remarks>
    /// Since <see cref="IFastEndpointsShellFeature"/> is a marker interface, we simply collect the assemblies
    /// containing the feature startup types without needing to instantiate the features.
    /// </remarks>
    private List<Assembly> DiscoverFastEndpointsAssemblies()
    {
        // Filter already-discovered features for those implementing IFastEndpointsShellFeature
        // and are enabled for this shell
        var fastEndpointsAssemblies = _allFeatureDescriptors
            .Where(d => d.StartupType != null &&
                        typeof(IFastEndpointsShellFeature).IsAssignableFrom(d.StartupType) &&
                        _shellSettings.EnabledFeatures.Contains(d.Id, StringComparer.OrdinalIgnoreCase))
            .Select(d => d.StartupType!.Assembly)
            .Distinct()
            .ToList();

        return fastEndpointsAssemblies;
    }
}
