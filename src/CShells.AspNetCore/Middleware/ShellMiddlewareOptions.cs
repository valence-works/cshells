namespace CShells.AspNetCore.Middleware;

/// <summary>
/// Configuration options for <see cref="ShellMiddleware"/>.
/// </summary>
public class ShellMiddlewareOptions
{
    /// <summary>
    /// Gets or sets whether shell resolution caching is enabled.
    /// Default is <c>true</c>.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the sliding expiration for cached shell resolutions.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan CacheSlidingExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the absolute expiration for cached shell resolutions.
    /// Default is 1 hour.
    /// </summary>
    public TimeSpan? CacheAbsoluteExpiration { get; set; } = TimeSpan.FromHours(1);
}
