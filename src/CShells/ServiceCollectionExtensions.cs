using System.Reflection;
using CShells.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // Register the root service collection accessor as early as possible.
            // This allows the shell host to copy root service registrations into each shell's service collection.
            // Note: The captured 'services' reference remains valid for the lifetime of the application.
            // Because IServiceCollection is mutable, any services added after AddCShells but before shells are built
            // will still be inherited by shells. This subtle behavior is correct but worth documenting for future maintainers.
            services.TryAddSingleton<IRootServiceCollectionAccessor>(
                _ => new RootServiceCollectionAccessor(services));

            var options = new CShellsOptions();
            configuration.GetSection(sectionName).Bind(options);

            // Validate that shells are configured
            if (options.Shells == null || !options.Shells.Any())
                throw new InvalidOperationException($"No shells configured in the configuration section '{sectionName}'.");
            // Convert configuration DTOs to runtime ShellSettings (may throw on invalid config).
            var shells = ShellSettingsFactory.CreateFromOptions(options).ToList();

            // Register the shell settings as a read-only collection for consumers.
            services.AddSingleton<IReadOnlyCollection<ShellSettings>>(shells.AsReadOnly());

            // Register IShellHost using the DefaultShellHost.
            // The root IServiceProvider is passed to allow IShellFeature constructors to resolve root-level services.
            // The root IServiceCollection is passed via the accessor to enable service inheritance in shells.
            services.AddSingleton<IShellHost>(sp =>
            {
                var logger = sp.GetService<ILogger<DefaultShellHost>>();
                var rootServicesAccessor = sp.GetRequiredService<IRootServiceCollectionAccessor>();
                var assembliesToScan = assemblies ?? AppDomain.CurrentDomain.GetAssemblies();
                return new DefaultShellHost(shells, assembliesToScan, rootProvider: sp, rootServicesAccessor, logger);
            });

            // Register the default shell context scope factory.
            services.AddSingleton<IShellContextScopeFactory, DefaultShellContextScopeFactory>();

            return services;
        }
    }
}
