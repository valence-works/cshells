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

            return builder.WithConfiguration("WebRouting:Path", path);
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

            return builder.WithConfiguration("WebRouting:Host", host);
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

            if (options.Path != null)
                builder.WithConfiguration("WebRouting:Path", options.Path);
            if (options.Host != null)
                builder.WithConfiguration("WebRouting:Host", options.Host);
            if (options.HeaderName != null)
                builder.WithConfiguration("WebRouting:HeaderName", options.HeaderName);
            if (options.ClaimKey != null)
                builder.WithConfiguration("WebRouting:ClaimKey", options.ClaimKey);

            return builder;
        }
    }
}
