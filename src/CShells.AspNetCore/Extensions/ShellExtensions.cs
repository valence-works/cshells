using System.Reflection;
using CShells.Configuration;
using CShells.DependencyInjection;
using CShells.Resolution;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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
            ArgumentNullException.ThrowIfNull(builder);

            return builder.AddShells(sectionName: CShellsOptions.SectionName, assemblies: null);
        }

        /// <summary>
        /// Adds CShells core services and ASP.NET Core integration using the default
        /// configuration section "CShells" and the specified feature assemblies.
        /// </summary>
        /// <param name="assemblies">The assemblies to scan for CShells features. If <c>null</c>, all loaded assemblies are scanned.</param>
        /// <returns>The same <see cref="WebApplicationBuilder"/> instance for chaining.</returns>
        public WebApplicationBuilder AddShells(IEnumerable<Assembly> assemblies)
        {
            ArgumentNullException.ThrowIfNull(builder);

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
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrEmpty(sectionName);

            return builder.AddShells(shells => shells.WithConfigurationProvider(builder.Configuration, sectionName), assemblies);
        }

        /// <summary>
        /// Adds CShells core services and ASP.NET Core integration, allowing customization
        /// of the shell settings provider and shell resolver.
        /// </summary>
        /// <param name="configureCShells">Callback used to configure the CShells builder (e.g., shell settings provider).</param>
        /// <param name="assemblies">The assemblies to scan for CShells features. If <c>null</c>, all loaded assemblies are scanned.</param>
        /// <returns>The same <see cref="WebApplicationBuilder"/> instance for chaining.</returns>        
        public WebApplicationBuilder AddShells(
            Action<CShellsBuilder> configureCShells,
            IEnumerable<Assembly>? assemblies = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configureCShells);

            // Register ASP.NET Core integration for CShells
            builder.Services.AddCShellsAspNetCore(configureCShells, assemblies);

            return builder;
        }
    }
}
