using CShells.Configuration;
using CShells.Resolution;

namespace CShells.AspNetCore.Resolution;

/// <summary>
/// A shell resolver strategy that determines the shell based on the HTTP host header.
/// Reads shell settings from the cache at runtime to find matching Host properties.
/// </summary>
[ResolverOrder(0)]
public class HostShellResolver : IShellResolverStrategy
{
    private readonly IShellSettingsCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostShellResolver"/> class.
    /// </summary>
    /// <param name="cache">The shell settings cache to read from.</param>
    public HostShellResolver(IShellSettingsCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    /// <inheritdoc />
    public ShellId? Resolve(ShellResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var host = context.Get<string>(ShellResolutionContextKeys.Host);
        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        // Search all shells for matching Host property
        foreach (var shell in _cache.GetAll())
        {
            if (shell.Properties.TryGetValue(ShellPropertyKeys.Host, out var hostValue))
            {
                var shellHost = hostValue switch
                {
                    string s => s,
                    System.Text.Json.JsonElement jsonElement when jsonElement.ValueKind == System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                    _ => null
                };

                if (shellHost != null && shellHost.Equals(host, StringComparison.OrdinalIgnoreCase))
                {
                    return shell.Id;
                }
            }
        }

        return null;
    }
}
