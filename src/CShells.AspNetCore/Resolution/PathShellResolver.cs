using CShells.Configuration;
using CShells.Resolution;

namespace CShells.AspNetCore.Resolution;

/// <summary>
/// A shell resolver strategy that determines the shell based on the first segment of the request URL path.
/// Reads shell settings from the cache at runtime to find matching Path properties.
/// </summary>
public class PathShellResolver : IShellResolverStrategy
{
    private readonly IShellSettingsCache _cache;
    private readonly PathShellResolverOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathShellResolver"/> class.
    /// </summary>
    /// <param name="cache">The shell settings cache to read from.</param>
    /// <param name="options">The options for path-based shell resolution.</param>
    public PathShellResolver(IShellSettingsCache cache, PathShellResolverOptions options)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        _cache = cache;
        _options = options;
    }

    /// <inheritdoc />
    public ShellId? Resolve(ShellResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var path = context.Get<string>(ShellResolutionContextKeys.Path);
        if (string.IsNullOrEmpty(path) || path.Length <= 1)
        {
            return null;
        }

        // Check if path is excluded from shell resolution
        if (_options.ExcludePaths != null && _options.ExcludePaths.Length > 0)
        {
            foreach (var excludedPath in _options.ExcludePaths)
            {
                if (path.StartsWith(excludedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return null; // Don't resolve shells for excluded paths
                }
            }
        }

        // Extract first segment (skip leading slash)
        var pathValue = path.AsSpan(1);
        var slashIndex = pathValue.IndexOf('/');
        var firstSegment = slashIndex >= 0 ? pathValue[..slashIndex].ToString() : pathValue.ToString();

        // Search all shells for matching Path property
        foreach (var shell in _cache.GetAll())
        {
            if (shell.Properties.TryGetValue(ShellPropertyKeys.Path, out var propertyValue))
            {
                var shellPath = propertyValue switch
                {
                    string s => s,
                    System.Text.Json.JsonElement jsonElement when jsonElement.ValueKind == System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                    _ => null
                };

                if (shellPath != null && shellPath.Equals(firstSegment, StringComparison.OrdinalIgnoreCase))
                {
                    return shell.Id;
                }
            }
        }

        return null;
    }
}
