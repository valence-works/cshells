namespace CShells.FastEndpoints.Options;

/// <summary>
/// Configuration options for FastEndpoints integration.
/// </summary>
public class FastEndpointsOptions
{
    /// <summary>
    /// Gets or sets the global route prefix to apply to all endpoints in this shell.
    /// </summary>
    /// <remarks>
    /// This prefix is applied in addition to any shell-level path prefix.
    /// For example, if the shell has a path prefix of "/tenant1" and this is set to "elsa/api",
    /// endpoints will be accessible at "/tenant1/elsa/api/...".
    /// Default value is null (no additional prefix).
    /// </remarks>
    public string? GlobalRoutePrefix { get; set; }
}
