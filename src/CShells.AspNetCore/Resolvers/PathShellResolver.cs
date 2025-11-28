using Microsoft.AspNetCore.Http;

namespace CShells.AspNetCore.Resolvers;

/// <summary>
/// A shell resolver that determines the shell based on the first segment of the request URL path.
/// </summary>
public class PathShellResolver : IShellResolver
{
    private readonly IReadOnlyDictionary<string, ShellId> _pathMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathShellResolver"/> class.
    /// </summary>
    /// <param name="pathMap">A dictionary mapping URL path prefixes to shell identifiers.
    /// Keys should be path segment values (e.g., "tenant1", "admin").</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pathMap"/> is null.</exception>
    public PathShellResolver(IReadOnlyDictionary<string, ShellId> pathMap)
    {
        ArgumentNullException.ThrowIfNull(pathMap);
        _pathMap = pathMap;
    }

    /// <inheritdoc />
    public ShellId? Resolve(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var path = httpContext.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        // Get the first path segment
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        var firstSegment = segments[0];

        if (_pathMap.TryGetValue(firstSegment, out var shellId))
        {
            return shellId;
        }

        return null;
    }
}
