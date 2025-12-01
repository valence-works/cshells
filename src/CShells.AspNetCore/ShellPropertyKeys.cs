namespace CShells.AspNetCore;

/// <summary>
/// Well-known property keys for shell settings used in ASP.NET Core scenarios.
/// These properties enable HTTP-specific shell resolution strategies.
/// </summary>
public static class ShellPropertyKeys
{
    /// <summary>
    /// Property key for the URL path prefix used to route requests to this shell.
    /// Example: "acme" maps requests like "/acme/..." to the shell.
    /// </summary>
    public const string Path = "AspNetCore.Path";

    /// <summary>
    /// Property key for the hostname used to route requests to this shell.
    /// Example: "acme.example.com" maps requests with this host header to the shell.
    /// </summary>
    public const string Host = "AspNetCore.Path";
}
