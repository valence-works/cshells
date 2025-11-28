using System.Reflection;
using CShells.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CShells
{
    /// <summary>
    /// ServiceCollection extensions for wiring CShells from IConfiguration.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers ShellSettings and a DefaultShellHost based on the configuration section.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="sectionName">The configuration section name (default: "CShells").</param>
        /// <param name="assemblies">Optional assemblies to scan for features. If null, scans all loaded assemblies.</param>
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddCShells(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "CShells",
            IEnumerable<Assembly>? assemblies = null)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));

            var options = new CShellsOptions();
            configuration.GetSection(sectionName).Bind(options);

            // Validate that shells are configured
            if (options.Shells == null || !options.Shells.Any())
                throw new InvalidOperationException($"No shells configured in the configuration section '{sectionName}'.");
            // Convert configuration DTOs to runtime ShellSettings (may throw on invalid config).
            var shells = ShellSettingsFactory.CreateFromOptions(options).ToList();

            // Register the shell settings as a read-only collection for consumers.
            services.AddSingleton<IReadOnlyCollection<ShellSettings>>(shells.AsReadOnly());

            // Register IShellHost using the DefaultShellHost; allow DI to provide ILogger<DefaultShellHost> if available.
            services.AddSingleton<IShellHost>(sp =>
            {
                var logger = sp.GetService<ILogger<DefaultShellHost>>();
                return assemblies is null
                    ? new DefaultShellHost(shells, logger)
                    : new DefaultShellHost(shells, assemblies, logger);
            });

            return services;
        }
    }
}
