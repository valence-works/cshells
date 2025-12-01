using System.Reflection;
using CShells.Configuration;
using CShells.Features;
using CShells.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CShells.DependencyInjection
{
    /// <summary>
    /// ServiceCollection extensions for registering CShells.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers CShells services and returns a builder for further configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="assemblies">Optional assemblies to scan for features. If null, scans all loaded assemblies.</param>
        /// <returns>A CShells builder for further configuration.</returns>
        public static CShellsBuilder AddCShells(
            this IServiceCollection services,
            IEnumerable<Assembly>? assemblies = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            // Register the root service collection accessor as early as possible.
            // This allows the shell host to copy root service registrations into each shell's service collection.
            // Note: The captured 'services' reference remains valid for the lifetime of the application.
            // Because IServiceCollection is mutable, any services added after AddCShells but before shells are built
            // will still be inherited by shells. This subtle behavior is correct but worth documenting for future maintainers.
            services.TryAddSingleton<IRootServiceCollectionAccessor>(
                _ => new RootServiceCollectionAccessor(services));

            // Register the feature factory for consistent feature instantiation across the framework
            services.TryAddSingleton<IShellFeatureFactory, DefaultShellFeatureFactory>();

            // Register the notification publisher for shell lifecycle events
            services.TryAddSingleton<Notifications.INotificationPublisher, Notifications.DefaultNotificationPublisher>();

            // Register the shell settings cache
            var cache = new ShellSettingsCache();
            services.TryAddSingleton<ShellSettingsCache>(cache);
            services.TryAddSingleton<IShellSettingsCache>(cache);

            // Register a hosted service that will populate the cache at startup
            // This ensures the cache is loaded when the application starts normally (via IHost.Run)
            services.AddHostedService<ShellSettingsCacheInitializer>();

            // Register IShellHost using the DefaultShellHost.
            // The root IServiceProvider is passed to allow IShellFeature constructors to resolve root-level services.
            // The root IServiceCollection is passed via the accessor to enable service inheritance in shells.
            // The cache is passed directly, and DefaultShellHost will call GetAll() at runtime.
            //
            // Note: The cache may be empty when IShellHost is constructed. This is OK - shells can be
            // loaded later via the hosted service or dynamically at runtime via the cache.
            services.AddSingleton<IShellHost>(sp =>
            {
                var shellCache = sp.GetRequiredService<ShellSettingsCache>();
                var logger = sp.GetService<ILogger<DefaultShellHost>>();
                var rootServicesAccessor = sp.GetRequiredService<IRootServiceCollectionAccessor>();
                var featureFactory = sp.GetRequiredService<IShellFeatureFactory>();
                var assembliesToScan = assemblies ?? AppDomain.CurrentDomain.GetAssemblies();

                return new DefaultShellHost(shellCache, assembliesToScan, rootProvider: sp, rootServicesAccessor, featureFactory, logger);
            });

            // Register the default shell context scope factory.
            services.AddSingleton<IShellContextScopeFactory, DefaultShellContextScopeFactory>();

            return new CShellsBuilder(services);
        }

        /// <summary>
        /// Registers CShells services with inline configuration and returns a builder for further configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Configuration action for the CShells builder.</param>
        /// <param name="assemblies">Optional assemblies to scan for features. If null, scans all loaded assemblies.</param>
        /// <returns>A CShells builder for further configuration.</returns>
        public static CShellsBuilder AddCShells(
            this IServiceCollection services,
            Action<CShellsBuilder> configure,
            IEnumerable<Assembly>? assemblies = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var builder = services.AddCShells(assemblies);
            configure(builder);
            return builder;
        }
    }
}
