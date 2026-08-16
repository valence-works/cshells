using CShells.AspNetCore.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CShells.DynamicFeatureFixture;

/// <summary>
/// A deliberately separate feature assembly used to prove that published endpoint delegates
/// do not retain a collectible feature load context after the generation is retired.
/// </summary>
public sealed class DynamicFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        endpoints.MapGet("/collectible-feature", HandleRequest);
    }

    private static IResult HandleRequest() => Results.Ok("collectible-feature");
}
