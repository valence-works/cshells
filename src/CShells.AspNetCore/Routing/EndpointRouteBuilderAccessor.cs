using Microsoft.AspNetCore.Routing;

namespace CShells.AspNetCore.Routing;

/// <summary>
/// Provides access to the IEndpointRouteBuilder captured during MapCShells().
/// This allows notification handlers to register endpoints after application startup.
/// </summary>
public class EndpointRouteBuilderAccessor
{
    /// <summary>
    /// Gets or sets the endpoint route builder.
    /// </summary>
    public IEndpointRouteBuilder? EndpointRouteBuilder { get; set; }
}
