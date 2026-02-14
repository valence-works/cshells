namespace CShells.AspNetCore;

/// <summary>
/// Configuration options for web routing-based shell resolution.
/// </summary>
public class WebRoutingShellOptions
{
    /// <summary>
    /// Gets or sets the URL path segment used to identify this shell.
    /// </summary>
    /// <remarks>
    /// This is used for shell resolution (determining which shell handles a request).
    /// For example, a path of "/tenant1" means requests to "/tenant1/*" will be routed to this shell.
    /// </remarks>
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets a route prefix applied to all endpoints registered in this shell.
    /// </summary>
    /// <remarks>
    /// This prefix is applied to all endpoint routes (minimal APIs, controllers, FastEndpoints, etc.).
    /// It is combined with the shell's <see cref="Path"/> to form the full route.
    /// For example, with Path="/tenant1" and RoutePrefix="api/v1", endpoints are at "/tenant1/api/v1/...".
    /// </remarks>
    public string? RoutePrefix { get; set; }

    /// <summary>
    /// Gets or sets the host name used to identify this shell.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// Gets or sets the HTTP header name to read for shell identification.
    /// </summary>
    public string? HeaderName { get; set; }

    /// <summary>
    /// Gets or sets the claim key to read from the authenticated user's claims for shell identification.
    /// </summary>
    public string? ClaimKey { get; set; }
}
