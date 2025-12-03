using CShells.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.DependencyInjection;

/// <summary>
/// Extension methods for configuring the CShellsBuilder.
/// </summary>
public static class CShellsBuilderExtensions
{
    /// <param name="builder">The CShells builder.</param>
    extension(CShellsBuilder builder)
    {
        /// <summary>
        /// Configures CShells to use a specific shell settings provider.
        /// </summary>
        /// <typeparam name="TProvider">The type of the shell settings provider.</typeparam>
        /// <returns>The updated CShells builder.</returns>
        public CShellsBuilder WithProvider<TProvider>()
            where TProvider : class, IShellSettingsProvider
        {
            Guard.Against.Null(builder);
            builder.Services.AddSingleton<IShellSettingsProvider, TProvider>();
            return builder;
        }

        /// <summary>
        /// Configures CShells to use a specific shell settings provider instance.
        /// </summary>
        /// <param name="provider">The shell settings provider instance.</param>
        /// <returns>The updated CShells builder.</returns>
        public CShellsBuilder WithProvider(IShellSettingsProvider provider)
        {
            Guard.Against.Null(builder);
            Guard.Against.Null(provider);
            builder.Services.AddSingleton(provider);
            return builder;
        }

        /// <summary>
        /// Configures CShells to use a specific shell settings provider factory.
        /// </summary>
        /// <param name="factory">The factory function to create the shell settings provider.</param>
        /// <returns>The updated CShells builder.</returns>
        public CShellsBuilder WithProvider(Func<IServiceProvider, IShellSettingsProvider> factory)
        {
            Guard.Against.Null(builder);
            Guard.Against.Null(factory);
            builder.Services.AddSingleton(factory);
            return builder;
        }

        /// <summary>
        /// Configures CShells to use the configuration-based shell settings provider.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="sectionName">The configuration section name (default: "CShells").</param>
        /// <returns>The updated CShells builder.</returns>
        public CShellsBuilder WithConfigurationProvider(IConfiguration configuration,
            string sectionName = CShellsOptions.SectionName)
        {
            Guard.Against.Null(builder);
            Guard.Against.Null(configuration);

            builder.Services.AddSingleton<IShellSettingsProvider>(
                _ => new ConfigurationShellSettingsProvider(configuration, sectionName));

            return builder;
        }
    }
}
