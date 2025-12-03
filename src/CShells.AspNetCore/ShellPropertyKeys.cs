namespace CShells.AspNetCore;

/// <summary>
/// Well-known property keys for shell settings used in ASP.NET Core scenarios.
/// These properties enable HTTP-specific shell resolution strategies.
/// </summary>
public static class ShellPropertyKeys
{
    /// <summary>
    /// Property key for web routing shell options that supports path, host, headers, and claims-based routing.
    /// Value should be a <see cref="WebRoutingShellOptions"/> object or compatible JSON structure.
    /// </summary>
    public const string WebRouting = "AspNetCore.WebRouting";
}
