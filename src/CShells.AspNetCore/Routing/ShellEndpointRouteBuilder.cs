using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace CShells.AspNetCore.Routing;

/// <summary>
/// An endpoint route builder that scopes all endpoints to a specific shell.
/// Routes are prefixed with the shell's path and tagged with shell metadata.
/// </summary>
public class ShellEndpointRouteBuilder : IEndpointRouteBuilder
{
    private readonly IEndpointRouteBuilder _inner;
    private readonly ShellId _shellId;
    private readonly ShellSettings _shellSettings;
    private readonly string? _pathPrefix;
    private readonly List<EndpointDataSource> _dataSources = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellEndpointRouteBuilder"/> class.
    /// </summary>
    /// <param name="inner">The inner endpoint route builder.</param>
    /// <param name="shellId">The shell ID.</param>
    /// <param name="shellSettings">The shell settings.</param>
    /// <param name="pathPrefix">Optional path prefix for all routes (e.g., "/acme").</param>
    public ShellEndpointRouteBuilder(
        IEndpointRouteBuilder inner,
        ShellId shellId,
        ShellSettings shellSettings,
        string? pathPrefix)
    {
        _inner = inner;
        _shellId = shellId;
        _shellSettings = shellSettings;
        _pathPrefix = pathPrefix;
    }

    /// <inheritdoc />
    public IServiceProvider ServiceProvider => _inner.ServiceProvider;

    /// <inheritdoc />
    public ICollection<EndpointDataSource> DataSources => _dataSources;

    /// <inheritdoc />
    public IApplicationBuilder CreateApplicationBuilder()
    {
        return _inner.CreateApplicationBuilder();
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
        if (!string.IsNullOrEmpty(_pathPrefix))
        {
            var prefixedPattern = RoutePatternFactory.Combine(
                RoutePatternFactory.Parse(_pathPrefix),
                pattern);
            pattern = prefixedPattern;
        }

        // Add shell metadata
        var metadata = new EndpointMetadataCollection(
            routeEndpoint.Metadata.Concat([new ShellEndpointMetadata(_shellId, _shellSettings)]));

        return new RouteEndpoint(
            routeEndpoint.RequestDelegate!,
            pattern,
            routeEndpoint.Order,
            metadata,
            routeEndpoint.DisplayName);
    }
}
