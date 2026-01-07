using CShells.Configuration;

namespace CShells.AspNetCore.Configuration;

/// <summary>
/// ASP.NET Core-specific extension methods for <see cref="ShellBuilder"/>.
/// </summary>
public static class ShellBuilderExtensions
{
    /// <param name="builder">The shell builder.</param>
    extension(ShellBuilder builder)
    {
        /// <summary>
        /// Configures the shell to be resolved by the specified URL path prefix.
        /// </summary>
        /// <param name="path">The URL path prefix (e.g., "acme" for /acme/...).</param>
        /// <returns>The builder for method chaining.</returns>
        public ShellBuilder WithPath(string path)
        {
            Guard.Against.Null(builder);
            Guard.Against.Null(path);

            var options = new WebRoutingShellOptions { Path = path };
            return builder.WithProperty(ShellPropertyKeys.WebRouting, options);
        }

        /// <summary>
        /// Configures the shell to be resolved by the specified hostname.
        /// </summary>
        /// <param name="host">The hostname (e.g., "acme.example.com").</param>
        /// <returns>The builder for method chaining.</returns>
        public ShellBuilder WithHost(string host)
        {
            Guard.Against.Null(builder);
            Guard.Against.Null(host);

            var options = new WebRoutingShellOptions { Host = host };
            return builder.WithProperty(ShellPropertyKeys.WebRouting, options);
        }

        /// <summary>
        /// Configures the shell with web routing options.
        /// </summary>
        /// <param name="options">The web routing options.</param>
        /// <returns>The builder for method chaining.</returns>
        public ShellBuilder WithWebRouting(WebRoutingShellOptions options)
        {
            Guard.Against.Null(builder);
            Guard.Against.Null(options);

            return builder.WithProperty(ShellPropertyKeys.WebRouting, options);
        }
    }
}
