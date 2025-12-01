using CShells.AspNetCore.Management;
using CShells.AspNetCore.Resolution;
using CShells.AspNetCore.Routing;
using CShells.DependencyInjection;
using CShells.Management;
using CShells.Resolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CShells.AspNetCore.Configuration;

/// <summary>
/// Extension methods for <see cref="CShellsBuilder"/> to configure ASP.NET Core-specific shell resolution.
/// </summary>
public static class CShellsBuilderExtensions
{
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
    public static CShellsBuilder WithAutoResolvers(this CShellsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register resolvers that read from the cache at runtime
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy, PathShellResolver>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy, HostShellResolver>());

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

        // Register the shell manager for runtime shell lifecycle management
        builder.Services.TryAddSingleton<IShellManager, DefaultShellManager>();

        // Register the endpoint route builder accessor to capture IEndpointRouteBuilder during MapCShells()
        builder.Services.TryAddSingleton<EndpointRouteBuilderAccessor>();

        // Register the endpoint registration notification handler
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<
            CShells.Notifications.INotificationHandler<CShells.Notifications.ShellAddedNotification>,
            Notifications.ShellEndpointRegistrationHandler>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<
            CShells.Notifications.INotificationHandler<CShells.Notifications.ShellRemovedNotification>,
            Notifications.ShellEndpointRegistrationHandler>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<
            CShells.Notifications.INotificationHandler<CShells.Notifications.ShellsReloadedNotification>,
            Notifications.ShellEndpointRegistrationHandler>());

        return builder;
    }
}

