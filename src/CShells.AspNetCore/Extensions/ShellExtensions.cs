using System.Reflection;
using CShells.Configuration;
using CShells.DependencyInjection;
using Microsoft.AspNetCore.Builder;

namespace CShells.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring CShells in ASP.NET Core applications.
/// </summary>
public static class ShellExtensions
{
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Adds CShells core services and ASP.NET Core integration using the default
        /// configuration section "CShells" and the default shell resolver.
        /// </summary>
        /// <returns>The same <see cref="WebApplicationBuilder"/> instance for chaining.</returns>
        public WebApplicationBuilder AddShells()
        {
            Guard.Against.Null(builder);

            return builder.AddShells(sectionName: CShellsOptions.SectionName, assemblies: null);
        }

        /// <summary>
        /// Adds CShells core services and ASP.NET Core integration using the default
        /// configuration section "CShells" and the default shell resolver.
        /// </summary>
        /// <param name="featureAssemblyMarkerTypes"> The types used to locate feature assemblies to scan for CShells features.</param>
        /// <returns>The same <see cref="WebApplicationBuilder"/> instance for chaining.</returns>
        public WebApplicationBuilder AddShells(IEnumerable<Type> featureAssemblyMarkerTypes)
        {
            var assemblyMarkerTypes = featureAssemblyMarkerTypes as Type[] ?? featureAssemblyMarkerTypes.ToArray();
            Guard.Against.Null(assemblyMarkerTypes);
            return builder.AddShells(assemblyMarkerTypes.Select(t => t.Assembly));
        }

        /// <summary>
        /// Adds CShells core services and ASP.NET Core integration using the default
        /// configuration section "CShells" and the specified feature assemblies.
        /// </summary>
        /// <param name="assemblies">The assemblies to scan for CShells features. If <c>null</c>, all loaded assemblies are scanned.</param>
        /// <returns>The same <see cref="WebApplicationBuilder"/> instance for chaining.</returns>
        public WebApplicationBuilder AddShells(IEnumerable<Assembly> assemblies)
        {
            Guard.Against.Null(builder);

            return builder.AddShells(sectionName: CShellsOptions.SectionName, assemblies: assemblies);
        }

        /// <summary>
        /// Adds CShells core services and ASP.NET Core integration using the specified
        /// configuration section and optional feature assemblies.
        /// </summary>
        /// <param name="sectionName">The configuration section name to bind CShells options from.</param>
        /// <param name="assemblies">The assemblies to scan for CShells features. If <c>null</c>, all loaded assemblies are scanned.</param>
        /// <returns>The same <see cref="WebApplicationBuilder"/> instance for chaining.</returns>
        public WebApplicationBuilder AddShells(string sectionName, IEnumerable<Assembly>? assemblies = null)
        {
            Guard.Against.Null(builder);
            Guard.Against.NullOrEmpty(sectionName);

            return builder.AddShells(shells => shells.WithConfigurationProvider(builder.Configuration, sectionName), assemblies);
        }

        /// <summary>
        /// Adds CShells core services and ASP.NET Core integration, allowing customization
        /// of the shell settings provider and shell resolver.
        /// </summary>
        /// <param name="configureCShells">Callback used to configure the CShells builder (e.g., shell settings provider).</param>
        /// <param name="assemblies">The assemblies to scan for CShells features. If <c>null</c>, all loaded assemblies are scanned.</param>
        /// <returns>The same <see cref="WebApplicationBuilder"/> instance for chaining.</returns>
        /// <remarks>
        /// If the configuration callback doesn't explicitly call <c>WithConfigurationProvider</c> or <c>WithProvider</c>,
        /// this method will automatically register the configuration-based provider using the default "CShells" section.
        /// </remarks>
        public WebApplicationBuilder AddShells(
            Action<CShellsBuilder> configureCShells,
            IEnumerable<Assembly>? assemblies = null)
        {
            Guard.Against.Null(builder);
            Guard.Against.Null(configureCShells);

            // Register ASP.NET Core integration for CShells
            builder.Services.AddCShellsAspNetCore(cshells =>
            {
                // Apply user configuration first
                configureCShells(cshells);

                // If no IShellSettingsProvider was registered, add the default configuration provider
                if (!cshells.Services.Any(d => d.ServiceType == typeof(IShellSettingsProvider)))
                {
                    cshells.WithConfigurationProvider(builder.Configuration);
                }
            }, assemblies);

            return builder;
        }
    }
}
