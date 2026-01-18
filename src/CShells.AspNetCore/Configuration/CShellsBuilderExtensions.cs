using CShells.AspNetCore.Authentication;
using CShells.AspNetCore.Authorization;
using CShells.AspNetCore.Resolution;
using CShells.AspNetCore.Routing;
using CShells.DependencyInjection;
using CShells.Notifications;
using CShells.Resolution;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
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
            builder.Services.AddSingleton<IShellResolverStrategy, WebRoutingShellResolver>();

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

            builder.Services.AddSingleton<IShellResolverStrategy, TStrategy>();

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

            builder.Services.AddSingleton(strategy);

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
            builder.Services.AddSingleton<INotificationHandler<ShellAddedNotification>, Notifications.ShellEndpointRegistrationHandler>();
            builder.Services.AddSingleton<INotificationHandler<ShellRemovedNotification>, Notifications.ShellEndpointRegistrationHandler>();
            builder.Services.AddSingleton<INotificationHandler<ShellsReloadedNotification>, Notifications.ShellEndpointRegistrationHandler>();

            return builder;
        }

        /// <summary>
        /// Enables shell-aware authentication that allows each shell to have its own authentication schemes.
        /// </summary>
        /// <returns>The builder for method chaining.</returns>
        /// <remarks>
        /// <para>
        /// This method registers <see cref="ShellAuthenticationSchemeProvider"/> which enables per-shell authentication schemes.
        /// Each shell can configure its own authentication schemes (e.g., JWT, API Key, Cookies) that work independently.
        /// </para>
        /// <para>
        /// <strong>How it works:</strong>
        /// The authentication middleware runs at the root level and uses the registered <see cref="IAuthenticationSchemeProvider"/>.
        /// <see cref="ShellAuthenticationSchemeProvider"/> intercepts scheme lookups and resolves them from the current shell's
        /// service provider, allowing each shell to have isolated authentication configurations.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // In Program.cs or Startup.cs
        /// services.AddAuthentication(); // Call FIRST
        /// services.AddCShellsAspNetCore(cshells => cshells
        ///     .WithShellAuthentication()
        /// );
        ///
        /// // In middleware pipeline
        /// app.UseRouting();
        /// app.UseAuthentication();
        /// app.MapShells();
        /// </code>
        /// </example>
        public CShellsBuilder WithAuthentication()
        {
            Guard.Against.Null(builder);

            // Register the shell-aware authentication scheme provider
            builder.Services.TryAddSingleton<IAuthenticationSchemeProvider, ShellAuthenticationSchemeProvider>();

            return builder;
        }

        /// <summary>
        /// Enables shell-aware authorization that allows each shell to have its own authorization policies.
        /// </summary>
        /// <returns>The builder for method chaining.</returns>
        /// <remarks>
        /// <para>
        /// This method registers <see cref="ShellAuthorizationPolicyProvider"/> which enables per-shell authorization policies.
        /// Each shell can configure its own authorization policies that work independently from other shells.
        /// </para>
        /// <para>
        /// <strong>How it works:</strong>
        /// The authorization middleware runs at the root level and uses the registered <see cref="IAuthorizationPolicyProvider"/>.
        /// <see cref="ShellAuthorizationPolicyProvider"/> intercepts policy lookups and resolves them from the current shell's
        /// service provider, allowing each shell to have isolated authorization policy configurations.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // In Program.cs or Startup.cs
        /// services.AddAuthorization();
        /// services.AddCShellsAspNetCore(cshells => cshells
        ///     .WithShellAuthorization()
        /// );
        ///
        /// // In middleware pipeline
        /// app.UseRouting();
        /// app.UseAuthorization(); // Call BEFORE MapShells
        /// app.MapShells();
        /// </code>
        /// </example>
        public CShellsBuilder WithAuthorization()
        {
            Guard.Against.Null(builder);

            // Register the shell-aware authorization policy provider
            builder.Services.TryAddSingleton<IAuthorizationPolicyProvider, ShellAuthorizationPolicyProvider>();

            return builder;
        }

        /// <summary>
        /// Enables both shell-aware authentication and authorization that allows each shell to have its own authentication schemes and authorization policies.
        /// </summary>
        /// <returns>The builder for method chaining.</returns>
        /// <remarks>
        /// <para>
        /// This is a convenience method that combines <see cref="WithAuthentication"/> and <see cref="WithAuthorization"/>.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // In Program.cs or Startup.cs
        /// services.AddAuthentication();
        /// services.AddAuthorization();
        /// services.AddCShellsAspNetCore(cshells => cshells
        ///     .WithAuthenticationAndAuthorization()
        /// );
        ///
        /// // In middleware pipeline
        /// app.UseRouting();
        /// app.UseAuthentication();
        /// app.UseAuthorization();
        /// app.MapShells();
        /// </code>
        /// </example>
        public CShellsBuilder WithAuthenticationAndAuthorization()
        {
            Guard.Against.Null(builder);

            builder.WithAuthentication();
            builder.WithAuthorization();

            return builder;
        }
    }
}

