using CShells.Configuration;
using CShells.Resolution;

namespace CShells.AspNetCore.Resolution;

/// <summary>
/// A unified shell resolver strategy that supports multiple routing methods:
/// URL path, HTTP host, custom headers, and user claims.
/// </summary>
[ResolverOrder(0)]
public class WebRoutingShellResolver(IShellSettingsCache cache, WebRoutingShellResolverOptions options) : IShellResolverStrategy
{
    private readonly IShellSettingsCache _cache = Guard.Against.Null(cache);
    private readonly WebRoutingShellResolverOptions _options = Guard.Against.Null(options);

    /// <inheritdoc />
    public ShellId? Resolve(ShellResolutionContext context)
    {
        Guard.Against.Null(context);
        return TryResolveByPath(context)
            ?? TryResolveByHost(context)
            ?? TryResolveByHeader(context)
            ?? TryResolveByClaim(context);
    }

    private ShellId? TryResolveByPath(ShellResolutionContext context)
    {
        if (!_options.EnablePathRouting)
            return null;

        var path = context.Get<string>(ShellResolutionContextKeys.Path);
        if (string.IsNullOrEmpty(path) || path.Length <= 1)
            return null;

        if (_options.ExcludePaths is { Length: > 0 } excludePaths)
        {
            if (excludePaths.Any(excludedPath => path.StartsWith(excludedPath, StringComparison.OrdinalIgnoreCase)))
                return null;
        }

        var pathValue = path.AsSpan(1);
        var slashIndex = pathValue.IndexOf('/');
        var firstSegment = slashIndex >= 0 ? pathValue[..slashIndex].ToString() : pathValue.ToString();

        return FindMatchingShell(firstSegment, "Path");
    }

    private ShellId? TryResolveByHost(ShellResolutionContext context)
    {
        if (!_options.EnableHostRouting)
            return null;

        var host = context.Get<string>(ShellResolutionContextKeys.Host);
        return string.IsNullOrEmpty(host) ? null : FindMatchingShell(host, "Host");
    }

    private ShellId? TryResolveByHeader(ShellResolutionContext context)
    {
        if (string.IsNullOrWhiteSpace(_options.HeaderName))
            return null;

        var headerValue = context.Get<string>($"Header:{_options.HeaderName}");
        return string.IsNullOrEmpty(headerValue) ? null : FindMatchingShellByIdentifier(headerValue, _options.HeaderName, "HeaderName");
    }

    private ShellId? TryResolveByClaim(ShellResolutionContext context)
    {
        if (string.IsNullOrWhiteSpace(_options.ClaimKey))
            return null;

        var claimValue = context.Get<string>($"Claim:{_options.ClaimKey}");
        return string.IsNullOrEmpty(claimValue) ? null : FindMatchingShellByIdentifier(claimValue, _options.ClaimKey, "ClaimKey");
    }

    private ShellId? FindMatchingShell(string valueToMatch, string configKey)
    {
        foreach (var shell in _cache.GetAll())
        {
            var routeValue = shell.GetConfiguration($"WebRouting:{configKey}");
            
            // If the path starts with a slash, throw a configuration exception:
            if (routeValue?.StartsWith('/') == true)
                throw new($"Web routing path cannot start with a slash: '{routeValue}'");
            
            if (!string.IsNullOrEmpty(routeValue) && routeValue.Equals(valueToMatch, StringComparison.OrdinalIgnoreCase))
                return shell.Id;
        }
        return null;
    }

    private ShellId? FindMatchingShellByIdentifier(string identifierValue, string configuredKey, string configKey)
    {
        foreach (var shell in _cache.GetAll())
        {
            var shellConfigKey = shell.GetConfiguration($"WebRouting:{configKey}");
            if (string.IsNullOrEmpty(shellConfigKey))
                continue;

            if (shellConfigKey.Equals(configuredKey, StringComparison.OrdinalIgnoreCase) &&
                identifierValue.Equals(shell.Id.Name, StringComparison.OrdinalIgnoreCase))
            {
                return shell.Id;
            }
        }
        return null;
    }
}
