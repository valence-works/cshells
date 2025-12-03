using CShells.AspNetCore.Resolution;
using CShells.Configuration;

namespace CShells.AspNetCore.Configuration;

/// <summary>
/// ASP.NET Core-specific extension methods for <see cref="ShellBuilder"/>.
/// </summary>
public static class ShellBuilderExtensions
{
    /// <summary>
    /// Configures the shell to be resolved by the specified URL path prefix.
    /// </summary>
    /// <param name="builder">The shell builder.</param>
    /// <param name="path">The URL path prefix (e.g., "acme" for /acme/...).</param>
    /// <returns>The builder for method chaining.</returns>
    public static ShellBuilder WithPath(this ShellBuilder builder, string path)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(path);

        var options = new WebRoutingShellOptions { Path = path };
        return builder.WithProperty(ShellPropertyKeys.WebRouting, options);
    }

    /// <summary>
    /// Configures the shell to be resolved by the specified hostname.
    /// </summary>
    /// <param name="builder">The shell builder.</param>
    /// <param name="host">The hostname (e.g., "acme.example.com").</param>
    /// <returns>The builder for method chaining.</returns>
    public static ShellBuilder WithHost(this ShellBuilder builder, string host)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(host);

        var options = new WebRoutingShellOptions { Host = host };
        return builder.WithProperty(ShellPropertyKeys.WebRouting, options);
    }

    /// <summary>
    /// Configures the shell with web routing options.
    /// </summary>
    /// <param name="builder">The shell builder.</param>
    /// <param name="options">The web routing options.</param>
    /// <returns>The builder for method chaining.</returns>
    public static ShellBuilder WithWebRouting(this ShellBuilder builder, WebRoutingShellOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        return builder.WithProperty(ShellPropertyKeys.WebRouting, options);
    }
}
