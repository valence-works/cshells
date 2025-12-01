using CShells.AspNetCore.Resolution;
using CShells.AspNetCore.Routing;
using CShells.DependencyInjection;
using CShells.Notifications;
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
    /// Registers the standard ASP.NET Core shell resolution strategies (Path and Host resolvers).
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
    /// For custom resolver strategies, use <see cref="WithResolverStrategy{TStrategy}"/> or <see cref="WithResolverStrategy(CShellsBuilder, IShellResolverStrategy)"/>.
    /// </remarks>
    public static CShellsBuilder WithStandardResolvers(this CShellsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register resolvers that read from the cache at runtime
        builder.WithPathResolver();
        builder.WithHostResolver();

        return builder;
    }

    /// <summary>
    /// Registers the standard ASP.NET Core shell resolution strategies (Path and Host resolvers).
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <remarks>
    /// This method is obsolete. Use <see cref="WithStandardResolvers"/> instead.
    /// </remarks>
    [Obsolete("Use WithStandardResolvers instead. This method will be removed in a future version.")]
    public static CShellsBuilder WithAutoResolvers(this CShellsBuilder builder)
    {
        return builder.WithStandardResolvers();
    }

    /// <summary>
    /// Registers a custom shell resolver strategy.
    /// </summary>
    /// <typeparam name="TStrategy">The type of the resolver strategy to register.</typeparam>
    /// <param name="builder">The CShells builder.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <remarks>
    /// Use this method to register custom resolver strategies such as claim-based, header-based,
    /// or any other custom resolution logic. The strategy will be added to the collection of
    /// resolver strategies that the shell resolver orchestrator will execute.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.AddCShells(cshells =>
    /// {
    ///     cshells.WithResolverStrategy&lt;ClaimBasedShellResolver&gt;();
    ///     cshells.WithResolverStrategy&lt;HeaderBasedShellResolver&gt;();
    /// });
    /// </code>
    /// </example>
    public static CShellsBuilder WithResolverStrategy<TStrategy>(this CShellsBuilder builder)
        where TStrategy : class, IShellResolverStrategy
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy, TStrategy>());

        return builder;
    }

    /// <summary>
    /// Registers a custom shell resolver strategy instance.
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <param name="strategy">The resolver strategy instance to register.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <remarks>
    /// Use this method to register a pre-configured instance of a custom resolver strategy.
    /// This is useful when you need to pass configuration or dependencies directly to the strategy instance.
    /// </remarks>
    /// <example>
    /// <code>
    /// var customResolver = new CustomShellResolver(someConfig);
    /// builder.AddCShells(cshells =>
    /// {
    ///     cshells.WithResolverStrategy(customResolver);
    /// });
    /// </code>
    /// </example>
    public static CShellsBuilder WithResolverStrategy(this CShellsBuilder builder, IShellResolverStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(strategy);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy>(strategy));

        return builder;
    }

    /// <summary>
    /// Registers ASP.NET Core endpoint routing services.
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <remarks>
    /// This method registers:
    /// <list type="bullet">
    /// <item><see cref="DynamicShellEndpointDataSource"/> for dynamic endpoint registration</item>
    /// <item>Notification handlers for shell lifecycle events to update endpoints automatically</item>
    /// </list>
    /// This must be called if you want to use endpoint routing with CShells.
    /// </remarks>
    public static CShellsBuilder WithEndpointRouting(this CShellsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register the dynamic endpoint data source as a singleton
        builder.Services.TryAddSingleton<DynamicShellEndpointDataSource>();

        // Register the endpoint route builder accessor to capture IEndpointRouteBuilder during MapCShells()
        builder.Services.TryAddSingleton<EndpointRouteBuilderAccessor>();

        // Register the endpoint registration notification handler
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationHandler<ShellAddedNotification>, Notifications.ShellEndpointRegistrationHandler>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationHandler<ShellRemovedNotification>, Notifications.ShellEndpointRegistrationHandler>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationHandler<ShellsReloadedNotification>, Notifications.ShellEndpointRegistrationHandler>());

        return builder;
    }
}

