using Microsoft.AspNetCore.Http;

namespace CShells.AspNetCore.Resolvers;

/// <summary>
/// A shell resolver that determines the shell based on the first segment of the request URL path.
/// </summary>
public class PathShellResolver : IShellResolver
{
    private readonly Dictionary<string, ShellId> _pathMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathShellResolver"/> class.
    /// </summary>
    /// <param name="pathMap">A dictionary mapping path segment names to shell identifiers.
    /// Keys should be the first path segment without leading slash (e.g., "tenant1", "admin").</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pathMap"/> is null.</exception>
    public PathShellResolver(IReadOnlyDictionary<string, ShellId> pathMap)
    {
        ArgumentNullException.ThrowIfNull(pathMap);
        _pathMap = new(pathMap, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public ShellId? Resolve(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var path = httpContext.Request.Path;
        if (!path.HasValue || path.Value!.Length <= 1)
        {
            return null;
        }

        // Extract first segment (skip leading slash)
        var pathValue = path.Value.AsSpan(1);
        var slashIndex = pathValue.IndexOf('/');
        var firstSegment = slashIndex >= 0 ? pathValue[..slashIndex].ToString() : pathValue.ToString();

        if (_pathMap.TryGetValue(firstSegment, out var shellId))
        {
            return shellId;
        }

        return null;
    }
}
