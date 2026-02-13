using System.Reflection;
using CShells.Configuration;
using CShells.Features;
using CShells.Hosting;
using CShells.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Logging;

namespace CShells.DependencyInjection;

/// <summary>
/// ServiceCollection extensions for registering CShells.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers CShells services and returns a builder for further configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action to customize the CShells builder.</param>
    /// <param name="assemblies">Optional assemblies to scan for features. If null, scans all loaded assemblies.</param>
    /// <returns>A CShells builder for further configuration.</returns>
    public static CShellsBuilder AddCShells(
        this IServiceCollection services,
        Action<CShellsBuilder>? configure,
        IEnumerable<Assembly>? assemblies = null)
    {
        Guard.Against.Null(services);

        // Register the root service collection accessor as early as possible.
        // This allows the shell host to copy root service registrations into each shell's service collection.
        // Note: The captured 'services' reference remains valid for the lifetime of the application.
        // Because IServiceCollection is mutable, any services added after AddCShells but before shells are built
        // will still be inherited by shells. This subtle behavior is correct but worth documenting for future maintainers.
        services.TryAddSingleton<IRootServiceCollectionAccessor>(
            _ => new RootServiceCollectionAccessor(services));

        // Register the default exclusion provider for core CShells infrastructure types
        services.AddSingleton<IShellServiceExclusionProvider, DefaultShellServiceExclusionProvider>();

        // Register the service exclusion registry (aggregates all providers)
        services.TryAddSingleton<Hosting.IShellServiceExclusionRegistry, Hosting.ShellServiceExclusionRegistry>();

        // Register the feature factory for consistent feature instantiation across the framework
        services.TryAddSingleton<IShellFeatureFactory, DefaultShellFeatureFactory>();

        // Register the notification publisher for shell lifecycle events
        services.TryAddSingleton<Notifications.INotificationPublisher, Notifications.DefaultNotificationPublisher>();

        // Register notification handlers for shell lifecycle events
        services.TryAddSingleton<Notifications.INotificationHandler<Notifications.ShellActivated>, Notifications.ShellActivationHandler>();
        services.TryAddSingleton<Notifications.INotificationHandler<Notifications.ShellDeactivating>, Notifications.ShellDeactivationHandler>();

        // Register the shell settings cache
        var cache = new ShellSettingsCache();
        services.TryAddSingleton<ShellSettingsCache>(cache);
        services.TryAddSingleton<IShellSettingsCache>(cache);

        // Register a hosted service that will populate the cache at startup
        // This ensures the cache is loaded when the application starts normally (via IHost.Run)
        services.AddHostedService<ShellSettingsCacheInitializer>();

        // Register hosted service for shell lifecycle coordination with app lifecycle
        services.AddHostedService<ShellStartupHostedService>();

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
            var exclusionRegistry = sp.GetRequiredService<Hosting.IShellServiceExclusionRegistry>();
            var assembliesToScan = assemblies ?? ResolveAssembliesToScan();

            return new DefaultShellHost(shellCache, assembliesToScan, rootProvider: sp, rootServicesAccessor, featureFactory, exclusionRegistry, logger);
        });

        // Register the default shell context scope factory.
        services.AddSingleton<IShellContextScopeFactory, DefaultShellContextScopeFactory>();

        // Register the shell manager for runtime shell lifecycle management
        services.TryAddSingleton<IShellManager, DefaultShellManager>();
            
        var builder = new CShellsBuilder(services);
        
        // Register the composite shell settings provider factory immediately
        // This must be done BEFORE configure is called so that DefaultShellManager can be constructed
        services.TryAddSingleton<IShellSettingsProvider>(sp =>
        {
            var providers = new List<IShellSettingsProvider>();
            
            // Add code-first shells provider if any shells were defined
            if (builder.CodeFirstShells.Count > 0)
            {
                providers.Add(new InMemoryShellSettingsProvider(builder.CodeFirstShells));
            }
            
            // Build and add all registered providers
            var registeredProviders = builder.BuildProviders(sp);
            providers.AddRange(registeredProviders);
            
            // If no providers were registered, return an empty provider
            if (providers.Count == 0)
            {
                return new InMemoryShellSettingsProvider([]);
            }
            
            // If only one provider, return it directly (optimization)
            if (providers.Count == 1)
            {
                return providers[0];
            }
            
            // Return composite provider for multiple providers
            return new CompositeShellSettingsProvider(providers);
        });
        
        configure?.Invoke(builder);
            
        return builder;
    }
    
    static IReadOnlyCollection<Assembly> ResolveAssembliesToScan(Func<AssemblyName, bool>? filter = null)
    {
        var entry = Assembly.GetEntryAssembly();
        var names = new HashSet<AssemblyName>(new AssemblyNameComparer());

        if (entry is not null)
            names.Add(entry.GetName());

        var dc = DependencyContext.Default;
        if (dc is not null)
        {
            foreach (var lib in dc.RuntimeLibraries)
            {
                foreach (var an in lib.GetDefaultAssemblyNames(dc))
                    names.Add(an);
            }
        }

        if (filter is not null)
            names.RemoveWhere(n => !filter(n));

        var loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            loaded[a.GetName().Name!] = a;

        var result = new List<Assembly>();
        foreach (var name in names)
        {
            if (name.Name is null)
                continue;

            if (loaded.TryGetValue(name.Name, out var alreadyLoaded))
            {
                result.Add(alreadyLoaded);
                continue;
            }

            try
            {
                result.Add(Assembly.Load(name));
            }
            catch
            {
                // Ignore assemblies that cannot be loaded in this process (optional deps, analyzers, etc.)
            }
        }

        return result;
    }

    sealed class AssemblyNameComparer : IEqualityComparer<AssemblyName>
    {
        public bool Equals(AssemblyName? x, AssemblyName? y) => StringComparer.OrdinalIgnoreCase.Equals(x?.Name, y?.Name);
        public int GetHashCode(AssemblyName obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name ?? string.Empty);
    }
}

