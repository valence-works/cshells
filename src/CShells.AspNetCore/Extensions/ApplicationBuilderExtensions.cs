using CShells.AspNetCore.Middleware;
using CShells.AspNetCore.Routing;
using CShells.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring CShells middleware and endpoint routing.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Configures CShells middleware and endpoints in the application pipeline.
    /// This must be called after UseRouting().
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The endpoint convention builder.</returns>
    /// <remarks>
    /// <para>
    /// This method configures dynamic endpoint routing for multi-tenant shell applications.
    /// It registers the shell resolution middleware and maps shell endpoints.
    /// Shells can be loaded at startup from configuration or asynchronously from storage,
    /// and can be added, removed, or updated at runtime without restarting the application.
    /// </para>
    /// <para>
    /// Proper usage:
    /// <code>
    /// app.UseRouting();
    /// app.MapCShells();
    /// </code>
    /// </para>
    /// </remarks>
    public static IEndpointConventionBuilder MapShells(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var logger = app.ApplicationServices.GetService<ILoggerFactory>()
            ?.CreateLogger(typeof(ApplicationBuilderExtensions))
            ?? NullLogger.Instance;

        logger.LogInformation("Configuring CShells middleware and endpoints");

        // Step 1: Add shell resolution middleware (sets current shell context on request)
        app.UseMiddleware<ShellMiddleware>();

        // Step 2: Cast to IEndpointRouteBuilder - WebApplication implements this interface
        var endpoints = app as IEndpointRouteBuilder
            ?? throw new InvalidOperationException(
                "MapCShells() requires UseRouting() to be called first. " +
                "Ensure app.UseRouting() is called before app.MapCShells().");

        // Step 3: Capture the endpoint route builder in the accessor so notification handlers can use it
        var accessor = endpoints.ServiceProvider.GetRequiredService<EndpointRouteBuilderAccessor>();
        accessor.EndpointRouteBuilder = endpoints;

        // Step 4: Get the dynamic endpoint data source
        var endpointDataSource = endpoints.ServiceProvider.GetRequiredService<DynamicShellEndpointDataSource>();

        // Step 5: Add the data source to the endpoint route builder
        // This makes all shell endpoints available to the routing system
        endpoints.DataSources.Add(endpointDataSource);

        logger.LogInformation("CShells middleware and endpoints configured successfully");

        // Return a convention builder (even though we don't have specific conventions to apply)
        return new EndpointConventionBuilder(endpointDataSource);
    }

    /// <summary>
    /// A simple endpoint convention builder for shell endpoints.
    /// </summary>
    private class EndpointConventionBuilder(DynamicShellEndpointDataSource dataSource) : IEndpointConventionBuilder
    {
        private readonly DynamicShellEndpointDataSource _dataSource = dataSource;

        public void Add(Action<EndpointBuilder> convention)
        {
            // Conventions can be applied to all endpoints in the data source
            // For now, we don't need to support this, but it's here for extensibility
        }
    }
}
