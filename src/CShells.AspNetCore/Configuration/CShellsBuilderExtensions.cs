using CShells.AspNetCore.Resolution;
using CShells.AspNetCore.Routing;
using CShells.DependencyInjection;
using CShells.Notifications;
using CShells.Resolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CShells.AspNetCore.Configuration;

/// <summary>
/// Extension methods for <see cref="CShellsBuilder"/> to configure ASP.NET Core-specific shell resolution.
/// </summary>
public static class CShellsBuilderExtensions
{
    /// <summary>
    /// Registers the unified web routing shell resolver with optional configuration.
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <param name="configure">Optional configuration action for web routing resolver options.</param>
    /// <param name="order">Optional execution order. If not specified, uses the order from <see cref="ResolverOrderAttribute"/> (default: 0).</param>
    /// <returns>The builder for method chaining.</returns>
    /// <remarks>
    /// This method registers <see cref="WebRoutingShellResolver"/> which supports multiple routing methods:
    /// path-based, host-based, header-based, and claim-based routing.
    /// Configure which methods are enabled via the options action.
    /// </remarks>
    public static CShellsBuilder WithWebRoutingResolver(this CShellsBuilder builder, Action<WebRoutingShellResolverOptions>? configure = null, int? order = null)
    {
        Guard.Against.Null(builder);

        var options = new WebRoutingShellResolverOptions();
        configure?.Invoke(options);

        builder.Services.TryAddSingleton(options);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy, WebRoutingShellResolver>());

        // Configure order if specified
        if (order.HasValue)
        {
            builder.Services.Configure<ShellResolverOptions>(opt => opt.SetOrder<WebRoutingShellResolver>(order.Value));
        }

        return builder;
    }

    /// <summary>
    /// Registers the standard ASP.NET Core shell resolution strategies.
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <remarks>
    /// This method registers the unified <see cref="WebRoutingShellResolver"/> with path and host routing enabled by default.
    /// The resolver supports multiple routing methods: URL path, HTTP host, custom headers, and user claims.
    /// For custom configuration, use <see cref="WithWebRoutingResolver"/> with a configuration action.
    /// </remarks>
    public static CShellsBuilder WithStandardResolvers(this CShellsBuilder builder)
    {
        Guard.Against.Null(builder);

        // Register the unified web routing resolver with default options
        builder.WithWebRoutingResolver();

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
    /// <param name="order">Optional execution order. If not specified, uses the order from <see cref="ResolverOrderAttribute"/> (default: 100).</param>
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
    ///     cshells.WithResolverStrategy&lt;HeaderBasedShellResolver&gt;(order: 50);
    /// });
    /// </code>
    /// </example>
    public static CShellsBuilder WithResolverStrategy<TStrategy>(this CShellsBuilder builder, int? order = null)
        where TStrategy : class, IShellResolverStrategy
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy, TStrategy>());

        // Configure order if specified
        if (order.HasValue)
        {
            builder.Services.Configure<ShellResolverOptions>(opt => opt.SetOrder<TStrategy>(order.Value));
        }

        return builder;
    }

    /// <summary>
    /// Registers a custom shell resolver strategy instance.
    /// </summary>
    /// <param name="builder">The CShells builder.</param>
    /// <param name="strategy">The resolver strategy instance to register.</param>
    /// <param name="order">Optional execution order. If not specified, uses the order from <see cref="ResolverOrderAttribute"/> (default: 100).</param>
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
    ///     cshells.WithResolverStrategy(customResolver, order: 75);
    /// });
    /// </code>
    /// </example>
    public static CShellsBuilder WithResolverStrategy(this CShellsBuilder builder, IShellResolverStrategy strategy, int? order = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(strategy);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy>(strategy));

        // Configure order if specified
        if (order.HasValue)
        {
            builder.Services.Configure<ShellResolverOptions>(opt => opt.SetOrder(strategy.GetType(), order.Value));
        }

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

