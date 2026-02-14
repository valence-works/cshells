namespace CShells.AspNetCore;

/// <summary>
/// Well-known configuration keys for shell settings used in ASP.NET Core scenarios.
/// These keys are used within the shell's ConfigurationData dictionary.
/// </summary>
public static class ShellConfigurationKeys
{
    /// <summary>
    /// Configuration key prefix for web routing options that supports path, host, headers, and claims-based routing.
    /// Keys under this prefix: WebRouting:Path, WebRouting:Host, WebRouting:HeaderName, WebRouting:ClaimKey.
    /// </summary>
    public const string WebRouting = "WebRouting";

    /// <summary>
    /// Configuration key for the web routing path.
    /// </summary>
    public const string WebRoutingPath = "WebRouting:Path";

    /// <summary>
    /// Configuration key for the web routing host.
    /// </summary>
    public const string WebRoutingHost = "WebRouting:Host";

    /// <summary>
    /// Configuration key for the web routing header name.
    /// </summary>
    public const string WebRoutingHeaderName = "WebRouting:HeaderName";

    /// <summary>
    /// Configuration key for the web routing claim key.
    /// </summary>
    public const string WebRoutingClaimKey = "WebRouting:ClaimKey";
}
