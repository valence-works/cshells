using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace CShells.AspNetCore.Routing;

/// <summary>
/// An endpoint route builder that scopes all endpoints to a specific shell.
/// Routes are prefixed with the shell's path and tagged with shell metadata.
/// </summary>
public class ShellEndpointRouteBuilder(
    IEndpointRouteBuilder inner,
    ShellId shellId,
    int generation,
    ShellSettings shellSettings,
    IServiceProvider shellContextServiceProvider,
    string? pathPrefix)
    : IEndpointRouteBuilder
{
    private readonly List<EndpointDataSource> _dataSources = [];
    private readonly Dictionary<Endpoint, string> _featureOwners = [];

    /// <inheritdoc />
    public IServiceProvider ServiceProvider { get; } = shellContextServiceProvider;

    /// <inheritdoc />
    public ICollection<EndpointDataSource> DataSources => _dataSources;

    /// <inheritdoc />
    public IApplicationBuilder CreateApplicationBuilder()
    {
        return inner.CreateApplicationBuilder();
    }

    /// <summary>
    /// Gets all endpoints with shell metadata and path prefixes applied.
    /// </summary>
    public IEnumerable<Endpoint> GetEndpoints()
    {
        foreach (var dataSource in _dataSources)
        {
            foreach (var endpoint in dataSource.Endpoints)
            {
                yield return ApplyShellMetadata(endpoint);
            }
        }
    }

    /// <summary>Gets the raw endpoint objects currently contributed by feature data sources.</summary>
    internal IReadOnlyList<Endpoint> GetRawEndpoints() =>
        [.._dataSources.SelectMany(dataSource => dataSource.Endpoints)];

    /// <summary>Associates newly mapped endpoints with their owning feature.</summary>
    internal void AssignFeature(string featureName, IEnumerable<Endpoint> endpoints)
    {
        Guard.Against.NullOrWhiteSpace(featureName);
        Guard.Against.Null(endpoints);

        foreach (var endpoint in endpoints)
            _featureOwners[endpoint] = featureName;
    }

    /// <summary>
    /// Applies shell metadata and path prefix to an endpoint.
    /// </summary>
    private Endpoint ApplyShellMetadata(Endpoint endpoint)
    {
        if (endpoint is not RouteEndpoint routeEndpoint)
            return endpoint;

        // Apply path prefix if configured
        var pattern = routeEndpoint.RoutePattern;
        if (!string.IsNullOrEmpty(pathPrefix))
        {
            var prefixedPattern = RoutePatternFactory.Combine(
                RoutePatternFactory.Parse(pathPrefix),
                pattern);
            pattern = prefixedPattern;
        }

        // Add shell metadata
        var featureName = _featureOwners.TryGetValue(endpoint, out var owner) ? owner : null;
        var metadata = new EndpointMetadataCollection(
            routeEndpoint.Metadata
                .Where(item => item is not ShellEndpointMetadata && item is not EndpointOwnershipMetadata)
                .Concat([
                new ShellEndpointMetadata(shellId, generation, shellSettings, featureName),
                new EndpointOwnershipMetadata(EndpointOwnerKind.DynamicShell, featureName ?? shellId.Name, shellId, generation),
                ]));

        return new RouteEndpoint(
            routeEndpoint.RequestDelegate!,
            pattern,
            routeEndpoint.Order,
            metadata,
            routeEndpoint.DisplayName);
    }
}
