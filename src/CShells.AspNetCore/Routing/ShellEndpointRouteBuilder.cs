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
    ShellSettings shellSettings,
    IServiceProvider shellContextServiceProvider,
    string? pathPrefix)
    : IEndpointRouteBuilder
{
    private readonly List<EndpointDataSource> _dataSources = [];

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
        var metadata = new EndpointMetadataCollection(
            routeEndpoint.Metadata.Concat([new ShellEndpointMetadata(shellId, shellSettings)]));

        return new RouteEndpoint(
            routeEndpoint.RequestDelegate!,
            pattern,
            routeEndpoint.Order,
            metadata,
            routeEndpoint.DisplayName);
    }
}
