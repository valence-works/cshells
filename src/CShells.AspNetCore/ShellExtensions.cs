namespace CShells.AspNetCore;

using System.Reflection;
using CShells;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring CShells in ASP.NET Core applications.
/// </summary>
public static class ShellExtensions
{
    /// <summary>
    /// Adds CShells core services and ASP.NET Core integration using the default
    /// configuration section "CShells" and the default shell resolver.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
    /// <returns>The same <see cref="WebApplicationBuilder"/> instance for chaining.</returns>
    public static WebApplicationBuilder AddCShells(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddCShells(sectionName: "CShells", assemblies: null);
    }

    /// <summary>
    /// Adds CShells core services and ASP.NET Core integration using the default
    /// configuration section "CShells" and the specified feature assemblies.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
    /// <param name="assemblies">The assemblies to scan for CShells features. If <c>null</c>, all loaded assemblies are scanned.</param>
    /// <returns>The same <see cref="WebApplicationBuilder"/> instance for chaining.</returns>
    public static WebApplicationBuilder AddCShells(this WebApplicationBuilder builder, IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddCShells(sectionName: "CShells", assemblies: assemblies);
    }

    /// <summary>
    /// Adds CShells core services and ASP.NET Core integration using the specified
    /// configuration section and optional feature assemblies.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
    /// <param name="sectionName">The configuration section name to bind CShells options from.</param>
    /// <param name="assemblies">The assemblies to scan for CShells features. If <c>null</c>, all loaded assemblies are scanned.</param>
    /// <returns>The same <see cref="WebApplicationBuilder"/> instance for chaining.</returns>
    public static WebApplicationBuilder AddCShells(this WebApplicationBuilder builder, string sectionName, IEnumerable<Assembly>? assemblies = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        ConfigureCoreAndAspNetCore(builder, sectionName, assemblies);
        return builder;
    }

    /// <summary>
    /// Adds CShells core services and ASP.NET Core integration using the specified
    /// configuration section and optional feature assemblies, allowing customization
    /// of the <see cref="IShellResolver"/> registration.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
    /// <param name="configureShellResolver">Callback used to configure shell resolver-related services, typically to register a custom <see cref="IShellResolver"/>.</param>
    /// <param name="sectionName">The configuration section name to bind CShells options from. Defaults to "CShells".</param>
    /// <param name="assemblies">The assemblies to scan for CShells features. If <c>null</c>, all loaded assemblies are scanned.</param>
    /// <returns>The same <see cref="WebApplicationBuilder"/> instance for chaining.</returns>
    public static WebApplicationBuilder AddCShells(
        this WebApplicationBuilder builder,
        Action<IServiceCollection> configureShellResolver,
        string sectionName = "CShells",
        IEnumerable<Assembly>? assemblies = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureShellResolver);
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        ConfigureCoreAndAspNetCore(builder, sectionName, assemblies);

        // Allow caller to override or customize IShellResolver registration before
        // the default resolver is added by AddCShellsAspNetCore.
        configureShellResolver(builder.Services);

        return builder;
    }

    private static void ConfigureCoreAndAspNetCore(
        WebApplicationBuilder builder,
        string sectionName,
        IEnumerable<Assembly>? assemblies)
    {
        IConfiguration configuration = builder.Configuration;
        IServiceCollection services = builder.Services;

        // Register CShells core services from configuration
        services.AddCShells(configuration, sectionName, assemblies);

        // Register ASP.NET Core integration for CShells (includes default IShellResolver
        // if none has been registered).
        services.AddCShellsAspNetCore();
    }
}
