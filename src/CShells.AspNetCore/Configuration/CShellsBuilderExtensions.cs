using CShells.AspNetCore.Management;
using CShells.AspNetCore.Resolution;
using CShells.AspNetCore.Routing;
using CShells.DependencyInjection;
using CShells.Management;
using CShells.Notifications;
using CShells.Resolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CShells.AspNetCore.Configuration;

// TODO: Consider calling WithEndpointRouting from ServiceCollectionExtensions.AddCShellsAspNetCore. The assumption being that when using ASPNET Core, well likely expose web shell features that configure endpoints.   

/// <summary>
/// Extension methods for <see cref="CShellsBuilder"/> to configure ASP.NET Core-specific shell resolution.
/// </summary>
public static class CShellsBuilderExtensions
{
    /// <summary>
    /// Registers the path-based shell resolver with optional configuration.
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <param name="configure">Optional configuration action for path resolver options.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <remarks>
    /// This method registers <see cref="PathShellResolver"/> to resolve shells by URL path segment.
    /// The resolver queries shell properties at runtime from the shell settings cache.
    /// </remarks>
    public static CShellsBuilder WithPathResolver(this CShellsBuilder builder, Action<PathShellResolverOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new PathShellResolverOptions();
        configure?.Invoke(options);

        builder.Services.TryAddSingleton(options);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy, PathShellResolver>());

        return builder;
    }

    /// <summary>
    /// Registers the host-based shell resolver.
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <remarks>
    /// This method registers <see cref="HostShellResolver"/> to resolve shells by HTTP host name.
    /// The resolver queries shell properties at runtime from the shell settings cache.
    /// </remarks>
    public static CShellsBuilder WithHostResolver(this CShellsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy, HostShellResolver>());

        return builder;
    }

    /// <summary>
    /// Registers ASP.NET Core shell resolution strategies that query Path and Host properties from shell settings.
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <remarks>
    /// This method registers:
    /// <list type="bullet">
    /// <item><see cref="PathShellResolver"/> to resolve shells by URL path segment</item>
    /// <item><see cref="HostShellResolver"/> to resolve shells by HTTP host name</item>
    /// </list>
    /// The resolvers query shell properties at runtime from the shell settings cache.
    /// </remarks>
    // TODO: Come up with a better name than "auto resolvers". Additionally, let's introduce a convenience extension method to register additional resolver strategies so that the user can easily register custom resolver strategies, such as claim based, header based, etc.
    public static CShellsBuilder WithAutoResolvers(this CShellsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register resolvers that read from the cache at runtime
        builder.WithPathResolver();
        builder.WithHostResolver();

        return builder;
    }

    /// <summary>
    /// Registers ASP.NET Core endpoint routing and shell management services.
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <remarks>
    /// This method registers:
    /// <list type="bullet">
    /// <item><see cref="DynamicShellEndpointDataSource"/> for dynamic endpoint registration</item>
    /// <item><see cref="IShellManager"/> for runtime shell management</item>
    /// </list>
    /// This must be called if you want to use endpoint routing with CShells.
    /// </remarks>
    public static CShellsBuilder WithEndpointRouting(this CShellsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register the dynamic endpoint data source as a singleton
        builder.Services.TryAddSingleton<DynamicShellEndpointDataSource>();

        // TODO: Move this to the DI code in the CShells project.
        // Register the shell manager for runtime shell lifecycle management
        builder.Services.TryAddSingleton<IShellManager, DefaultShellManager>();

        // Register the endpoint route builder accessor to capture IEndpointRouteBuilder during MapCShells()
        builder.Services.TryAddSingleton<EndpointRouteBuilderAccessor>();

        // Register the endpoint registration notification handler
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationHandler<ShellAddedNotification>, Notifications.ShellEndpointRegistrationHandler>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationHandler<ShellRemovedNotification>, Notifications.ShellEndpointRegistrationHandler>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationHandler<ShellsReloadedNotification>, Notifications.ShellEndpointRegistrationHandler>());

        return builder;
    }
}

