namespace CShells.AspNetCore.Resolution;

/// <summary>
/// Configuration options for the unified web routing shell resolver.
/// Enable or disable specific routing methods by setting the corresponding properties.
/// </summary>
public class WebRoutingShellResolverOptions
{
    /// <summary>
    /// Gets or sets whether path-based routing is enabled.
    /// When true, shells can be resolved by the first URL path segment.
    /// Default is true.
    /// </summary>
    public bool EnablePathRouting { get; set; } = true;

    /// <summary>
    /// Gets or sets whether host-based routing is enabled.
    /// When true, shells can be resolved by the HTTP Host header.
    /// Default is true.
    /// </summary>
    public bool EnableHostRouting { get; set; } = true;

    /// <summary>
    /// Gets or sets the HTTP header name to use for header-based routing.
    /// When set, shells can be resolved by reading this header value.
    /// Example: "X-Tenant-Id"
    /// </summary>
    public string? HeaderName { get; set; }

    /// <summary>
    /// Gets or sets the claim key to use for claim-based routing.
    /// When set, shells can be resolved by reading this claim from the authenticated user.
    /// Example: "tenant_id"
    /// </summary>
    public string? ClaimKey { get; set; }

    /// <summary>
    /// Gets or sets paths that should be excluded from shell resolution.
    /// Requests starting with these paths will not trigger shell resolution.
    /// Useful for excluding static files, health checks, etc.
    /// </summary>
    public string[]? ExcludePaths { get; set; }
}
