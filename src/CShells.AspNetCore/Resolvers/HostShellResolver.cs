using Microsoft.AspNetCore.Http;

namespace CShells.AspNetCore.Resolvers;

/// <summary>
/// A shell resolver that determines the shell based on the HTTP Host header.
/// </summary>
public class HostShellResolver : IShellResolver
{
    private readonly Dictionary<string, ShellId> _hostMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostShellResolver"/> class.
    /// </summary>
    /// <param name="hostMap">A dictionary mapping host names to shell identifiers.
    /// Keys should be host names (e.g., "tenant1.example.com", "localhost"). Matching is case-insensitive.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hostMap"/> is null.</exception>
    public HostShellResolver(IReadOnlyDictionary<string, ShellId> hostMap)
    {
        ArgumentNullException.ThrowIfNull(hostMap);
        _hostMap = new(hostMap, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public ShellId? Resolve(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var host = httpContext.Request.Host.Host;
        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        if (_hostMap.TryGetValue(host, out var shellId))
        {
            return shellId;
        }

        return null;
    }
}
