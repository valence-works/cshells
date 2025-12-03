namespace CShells.AspNetCore;

/// <summary>
/// Configuration options for web routing-based shell resolution.
/// </summary>
public class WebRoutingShellOptions
{
    /// <summary>
    /// Gets or sets the URL path segment used to identify this shell.
    /// </summary>
    public string? Path { get; set; }

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
