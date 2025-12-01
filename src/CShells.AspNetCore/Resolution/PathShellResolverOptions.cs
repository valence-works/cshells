namespace CShells.AspNetCore.Resolution;

/// <summary>
/// Configuration options for <see cref="PathShellResolver"/>.
/// </summary>
public class PathShellResolverOptions
{
    /// <summary>
    /// Gets or sets the list of path prefixes that should be excluded from shell resolution.
    /// Requests matching these paths will not resolve to any shell.
    /// </summary>
    /// <example>
    /// <code>
    /// options.ExcludePaths = new[] { "/api", "/admin", "/health", "/swagger" };
    /// </code>
    /// </example>
    public string[] ExcludePaths { get; set; } = [];
}
