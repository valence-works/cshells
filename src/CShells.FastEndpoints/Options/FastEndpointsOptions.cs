namespace CShells.FastEndpoints.Options;

/// <summary>
/// Configuration options for FastEndpoints integration.
/// </summary>
public class FastEndpointsOptions
{
    /// <summary>
    /// Gets or sets a route prefix applied specifically to FastEndpoints in this shell.
    /// </summary>
    /// <remarks>
    /// This prefix is applied via FastEndpoints' native <c>config.Endpoints.RoutePrefix</c> setting.
    /// It is applied in addition to any shell-level route prefix (configured via <c>WebRouting:RoutePrefix</c>).
    /// For example, if the shell has a path of "/tenant1", a WebRouting:RoutePrefix of "api",
    /// and this is set to "v2", endpoints will be accessible at "/tenant1/api/v2/...".
    /// Default value is null (no additional prefix).
    /// </remarks>
    public string? EndpointRoutePrefix { get; set; }
}
