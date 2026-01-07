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
    /// <param name="builder">The CShells builder.</param>
    extension(CShellsBuilder builder)
    {
        /// <summary>
        /// Configures the shell resolver to use web routing strategies (path, host, header, and claim-based routing).
        /// This is a convenience method that configures the resolver pipeline with <see cref="WebRoutingShellResolver"/>.
        /// </summary>
        /// <param name="configure">Optional configuration action for web routing resolver options.</param>
        /// <returns>The builder for method chaining.</returns>
        /// <remarks>
        /// This method configures the resolver pipeline with <see cref="WebRoutingShellResolver"/> which supports
        /// multiple routing methods: path-based, host-based, header-based, and claim-based routing.
        /// Configure which methods are enabled via the options action.
        /// </remarks>
        /// <example>
        /// <code>
        /// builder.AddShells(shells => shells
        ///     .WithWebRouting(options =>
        ///     {
        ///         options.HeaderName = "X-Tenant-Id";
        ///         options.EnablePathRouting = true;
        ///     })
        /// );
        /// </code>
        /// </example>
        public CShellsBuilder WithWebRouting(Action<WebRoutingShellResolverOptions>? configure = null)
        {
            Guard.Against.Null(builder);

            var options = new WebRoutingShellResolverOptions();
            configure?.Invoke(options);

            builder.Services.TryAddSingleton(options);

            return builder.ConfigureResolverPipeline(pipeline => pipeline
                .Use<WebRoutingShellResolver>(order: 0));
        }

        /// <summary>
        /// Registers the unified web routing shell resolver with optional configuration.
        /// </summary>
        /// <param name="configure">Optional configuration action for web routing resolver options.</param>
        /// <param name="order">Optional execution order. If not specified, uses the order from <see cref="ResolverOrderAttribute"/> (default: 0).</param>
        /// <returns>The builder for method chaining.</returns>
        /// <remarks>
        /// This method is obsolete. Use <see cref="WithWebRouting"/> instead for a simpler API.
        /// </remarks>
        [Obsolete("Use WithWebRouting instead. This method will be removed in a future version.")]
        public CShellsBuilder WithWebRoutingResolver(Action<WebRoutingShellResolverOptions>? configure = null, int? order = null)
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
        /// <returns>The builder for method chaining.</returns>
        /// <remarks>
        /// This method is obsolete. Use <see cref="WithWebRouting"/> instead for explicit configuration.
        /// </remarks>
        [Obsolete("Use WithWebRouting instead. This method will be removed in a future version.")]
        public CShellsBuilder WithStandardResolvers()
        {
            return builder.WithWebRouting();
        }

        /// <summary>
        /// Registers the standard ASP.NET Core shell resolution strategies (Path and Host resolvers).
        /// </summary>
        /// <returns>The builder for method chaining.</returns>
        /// <remarks>
        /// This method is obsolete. Use <see cref="WithWebRouting"/> instead for explicit configuration.
        /// </remarks>
        [Obsolete("Use WithWebRouting instead. This method will be removed in a future version.")]
        public CShellsBuilder WithAutoResolvers()
        {
            return builder.WithWebRouting();
        }

        /// <summary>
        /// Registers a custom shell resolver strategy.
        /// </summary>
        /// <typeparam name="TStrategy">The type of the resolver strategy to register.</typeparam>
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
        public CShellsBuilder WithResolverStrategy<TStrategy>(int? order = null)
            where TStrategy : class, IShellResolverStrategy
        {
            Guard.Against.Null(builder);

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
        public CShellsBuilder WithResolverStrategy(IShellResolverStrategy strategy, int? order = null)
        {
            Guard.Against.Null(builder);
            Guard.Against.Null(strategy);

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
        /// <returns>The builder for method chaining.</returns>
        /// <remarks>
        /// This method registers:
        /// <list type="bullet">
        /// <item><see cref="DynamicShellEndpointDataSource"/> for dynamic endpoint registration</item>
        /// <item>Notification handlers for shell lifecycle events to update endpoints automatically</item>
        /// </list>
        /// This must be called if you want to use endpoint routing with CShells.
        /// </remarks>
        public CShellsBuilder WithEndpointRouting()
        {
            Guard.Against.Null(builder);

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
}

