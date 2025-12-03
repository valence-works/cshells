using CShells.Configuration;
using CShells.Resolution;

namespace CShells.AspNetCore.Resolution;

/// <summary>
/// A unified shell resolver strategy that supports multiple routing methods:
/// URL path, HTTP host, custom headers, and user claims.
/// Configure which methods to use via <see cref="WebRoutingShellResolverOptions"/>.
/// </summary>
[ResolverOrder(0)]
public class WebRoutingShellResolver : IShellResolverStrategy
{
    private readonly IShellSettingsCache _cache;
    private readonly WebRoutingShellResolverOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebRoutingShellResolver"/> class.
    /// </summary>
    /// <param name="cache">The shell settings cache to read from.</param>
    /// <param name="options">The options controlling which routing methods are enabled.</param>
    public WebRoutingShellResolver(IShellSettingsCache cache, WebRoutingShellResolverOptions options)
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

        // Try each enabled routing method in order
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

        // Check if path is excluded from shell resolution
        if (_options.ExcludePaths != null && _options.ExcludePaths.Length > 0)
        {
            foreach (var excludedPath in _options.ExcludePaths)
            {
                if (path.StartsWith(excludedPath, StringComparison.OrdinalIgnoreCase))
                    return null;
            }
        }

        // Extract first segment (skip leading slash)
        var pathValue = path.AsSpan(1);
        var slashIndex = pathValue.IndexOf('/');
        var firstSegment = slashIndex >= 0 ? pathValue[..slashIndex].ToString() : pathValue.ToString();

        return FindMatchingShell(
            valueToMatch: firstSegment,
            getRouteValue: options => options.Path
        );
    }

    private ShellId? TryResolveByHost(ShellResolutionContext context)
    {
        if (!_options.EnableHostRouting)
            return null;

        var host = context.Get<string>(ShellResolutionContextKeys.Host);
        if (string.IsNullOrEmpty(host))
            return null;

        return FindMatchingShell(
            valueToMatch: host,
            getRouteValue: options => options.Host
        );
    }

    private ShellId? TryResolveByHeader(ShellResolutionContext context)
    {
        if (string.IsNullOrWhiteSpace(_options.HeaderName))
            return null;

        var headerValue = context.Get<string>($"Header:{_options.HeaderName}");
        if (string.IsNullOrEmpty(headerValue))
            return null;

        return FindMatchingShellByIdentifier(
            identifierValue: headerValue,
            configuredKey: _options.HeaderName,
            getConfigKey: options => options.HeaderName
        );
    }

    private ShellId? TryResolveByClaim(ShellResolutionContext context)
    {
        if (string.IsNullOrWhiteSpace(_options.ClaimKey))
            return null;

        var claimValue = context.Get<string>($"Claim:{_options.ClaimKey}");
        if (string.IsNullOrEmpty(claimValue))
            return null;

        return FindMatchingShellByIdentifier(
            identifierValue: claimValue,
            configuredKey: _options.ClaimKey,
            getConfigKey: options => options.ClaimKey
        );
    }

    /// <summary>
    /// Finds a shell by matching a value against shell routing configuration.
    /// Used for path and host routing where the value must match the configured route value.
    /// </summary>
    private ShellId? FindMatchingShell(
        string valueToMatch,
        Func<WebRoutingShellOptions, string?> getRouteValue)
    {
        foreach (var shell in _cache.GetAll())
        {
            var routingOptions = shell.GetProperty<WebRoutingShellOptions>(ShellPropertyKeys.WebRouting);
            if (routingOptions != null)
            {
                var routeValue = getRouteValue(routingOptions);
                if (!string.IsNullOrEmpty(routeValue) &&
                    routeValue.Equals(valueToMatch, StringComparison.OrdinalIgnoreCase))
                {
                    return shell.Id;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a shell by matching an identifier (header/claim value) against the shell name.
    /// Used for header and claim routing where the identifier value must match the shell ID.
    /// </summary>
    private ShellId? FindMatchingShellByIdentifier(
        string identifierValue,
        string configuredKey,
        Func<WebRoutingShellOptions, string?> getConfigKey)
    {
        foreach (var shell in _cache.GetAll())
        {
            var routingOptions = shell.GetProperty<WebRoutingShellOptions>(ShellPropertyKeys.WebRouting);
            if (routingOptions != null)
            {
                var configKey = getConfigKey(routingOptions);
                if (!string.IsNullOrEmpty(configKey) &&
                    configKey.Equals(configuredKey, StringComparison.OrdinalIgnoreCase) &&
                    identifierValue.Equals(shell.Id.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return shell.Id;
                }
            }
        }

        return null;
    }
}
