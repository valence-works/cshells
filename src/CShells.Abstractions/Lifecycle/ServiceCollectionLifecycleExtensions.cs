using Microsoft.Extensions.DependencyInjection;

namespace CShells.Lifecycle;

/// <summary>
/// Extension methods for registering shell lifecycle components.
/// </summary>
public static class ServiceCollectionLifecycleExtensions
{
    /// <summary>
    /// Registers a transient shell initializer in <see cref="LifecyclePhase.Default"/> with order <c>0</c>.
    /// </summary>
    /// <typeparam name="TInitializer">The initializer implementation type.</typeparam>
    /// <param name="services">The shell service collection to register into.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This is the first-class equivalent of registering a transient
    /// <see cref="IShellInitializer"/> directly, with default-phase lifecycle metadata attached.
    /// The initializer runs in <see cref="LifecyclePhase.Default"/> after any
    /// <see cref="LifecyclePhase.Prepare"/> initializers and before any
    /// <see cref="LifecyclePhase.Start"/> initializers.
    /// <code>
    /// services.AddShellInitializer&lt;WarmCacheInitializer&gt;();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddShellInitializer<TInitializer>(this IServiceCollection services)
        where TInitializer : class, IShellInitializer
        => AddShellInitializerCore<TInitializer>(
            services,
            LifecyclePhase.Default,
            order: 0,
            isExplicit: false,
            source: $"AddShellInitializer<{typeof(TInitializer).FullName}> (default)");

    /// <summary>
    /// Registers a transient shell initializer in <see cref="LifecyclePhase.Default"/>.
    /// </summary>
    /// <typeparam name="TInitializer">The initializer implementation type.</typeparam>
    /// <param name="services">The shell service collection to register into.</param>
    /// <param name="order">The numeric order within <see cref="LifecyclePhase.Default"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Use this overload when an initializer should stay in the compatibility phase but run
    /// before or after other default-phase initializers.
    /// <code>
    /// services.AddShellInitializer&lt;WarmCacheInitializer&gt;(order: 100);
    /// </code>
    /// </remarks>
    public static IServiceCollection AddShellInitializer<TInitializer>(
        this IServiceCollection services,
        int order)
        where TInitializer : class, IShellInitializer =>
        services.AddShellInitializer<TInitializer>(LifecyclePhase.Default, order);

    /// <summary>
    /// Registers a transient shell initializer in the specified lifecycle phase.
    /// </summary>
    /// <typeparam name="TInitializer">The initializer implementation type.</typeparam>
    /// <param name="services">The shell service collection to register into.</param>
    /// <param name="phase">The semantic lifecycle phase.</param>
    /// <param name="order">The numeric order within <paramref name="phase"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Initializers registered through this API are resolved from the shell's
    /// <see cref="IServiceProvider"/> at activation time, so they may depend on shell-scoped
    /// services. Explicit metadata from this registration overrides any
    /// <see cref="LifecycleOrderAttribute"/> on <typeparamref name="TInitializer"/>.
    /// <typeparamref name="TInitializer"/> is registered as transient and exposed as
    /// <see cref="IShellInitializer"/> without replacing any existing descriptors.
    /// <code>
    /// services.AddShellInitializer&lt;ApplyMigrationsInitializer&gt;(LifecyclePhase.Prepare, order: 100);
    /// services.AddShellInitializer&lt;StartSchedulerInitializer&gt;(LifecyclePhase.Start, order: 100);
    /// </code>
    /// As an alternative for legacy registrations, apply
    /// <see cref="LifecycleOrderAttribute"/> to the initializer implementation type.
    /// </remarks>
    public static IServiceCollection AddShellInitializer<TInitializer>(
        this IServiceCollection services,
        LifecyclePhase phase,
        int order)
        where TInitializer : class, IShellInitializer
        => AddShellInitializerCore<TInitializer>(
            services,
            phase,
            order,
            isExplicit: true,
            source: $"AddShellInitializer<{typeof(TInitializer).FullName}>");

    private static IServiceCollection AddShellInitializerCore<TInitializer>(
        IServiceCollection services,
        LifecyclePhase phase,
        int order,
        bool isExplicit,
        string source)
        where TInitializer : class, IShellInitializer
    {
        Guard.Against.Null(services);

        var registrationIndex = services.Count(d => d.ServiceType == typeof(IShellInitializer));
        services.AddTransient<TInitializer>();
        services.AddTransient<IShellInitializer>(sp => sp.GetRequiredService<TInitializer>());
        services.AddSingleton(new ShellInitializerRegistration(
            typeof(TInitializer),
            phase,
            order,
            registrationIndex,
            isExplicit,
            source));

        return services;
    }

    /// <summary>
    /// Registers a transient shell terminator in <see cref="LifecyclePhase.Default"/> with order <c>0</c>.
    /// </summary>
    /// <typeparam name="TTerminator">The terminator implementation type.</typeparam>
    /// <param name="services">The shell service collection to register into.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This is the first-class equivalent of registering a transient
    /// <see cref="IShellTerminator"/> directly, with default-phase lifecycle metadata attached.
    /// Because terminators execute phases mirror-reversed, the terminator runs in
    /// <see cref="LifecyclePhase.Default"/> after any <see cref="LifecyclePhase.Start"/>
    /// terminators and before any <see cref="LifecyclePhase.Prepare"/> terminators.
    /// <code>
    /// services.AddShellTerminator&lt;FlushCacheTerminator&gt;();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddShellTerminator<TTerminator>(this IServiceCollection services)
        where TTerminator : class, IShellTerminator
        => AddShellTerminatorCore<TTerminator>(
            services,
            LifecyclePhase.Default,
            order: 0,
            isExplicit: false,
            source: $"AddShellTerminator<{typeof(TTerminator).FullName}> (default)");

    /// <summary>
    /// Registers a transient shell terminator in <see cref="LifecyclePhase.Default"/>.
    /// </summary>
    /// <typeparam name="TTerminator">The terminator implementation type.</typeparam>
    /// <param name="services">The shell service collection to register into.</param>
    /// <param name="order">The numeric order within <see cref="LifecyclePhase.Default"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Use this overload when a terminator should stay in the compatibility phase but run
    /// before or after other default-phase terminators. Higher orders run first during
    /// termination (mirror of initializer ordering).
    /// <code>
    /// services.AddShellTerminator&lt;FlushCacheTerminator&gt;(order: 100);
    /// </code>
    /// </remarks>
    public static IServiceCollection AddShellTerminator<TTerminator>(
        this IServiceCollection services,
        int order)
        where TTerminator : class, IShellTerminator =>
        services.AddShellTerminator<TTerminator>(LifecyclePhase.Default, order);

    /// <summary>
    /// Registers a transient shell terminator in the specified lifecycle phase.
    /// </summary>
    /// <typeparam name="TTerminator">The terminator implementation type.</typeparam>
    /// <param name="services">The shell service collection to register into.</param>
    /// <param name="phase">The semantic lifecycle phase.</param>
    /// <param name="order">The numeric order within <paramref name="phase"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Terminators registered through this API are resolved from the shell's
    /// <see cref="IServiceProvider"/> during graceful drain — after the shell reaches
    /// <see cref="ShellLifecycleState.Drained"/> and before its provider is disposed — so they
    /// may depend on any shell service. Terminators execute mirror-reversed relative to
    /// initializers: a terminator registered at the same phase and order as an initializer
    /// tears down at the mirrored point (<see cref="LifecyclePhase.Start"/> first,
    /// <see cref="LifecyclePhase.Prepare"/> last, descending order within a phase). Explicit
    /// metadata from this registration overrides any <see cref="LifecycleOrderAttribute"/> on
    /// <typeparamref name="TTerminator"/>. <typeparamref name="TTerminator"/> is registered as
    /// transient and exposed as <see cref="IShellTerminator"/> without replacing any existing
    /// descriptors.
    /// <code>
    /// services.AddShellInitializer&lt;StartSchedulerInitializer&gt;(LifecyclePhase.Start, order: 100);
    /// services.AddShellTerminator&lt;StopSchedulerTerminator&gt;(LifecyclePhase.Start, order: 100);
    /// </code>
    /// As an alternative for legacy registrations, apply
    /// <see cref="LifecycleOrderAttribute"/> to the terminator implementation type.
    /// </remarks>
    public static IServiceCollection AddShellTerminator<TTerminator>(
        this IServiceCollection services,
        LifecyclePhase phase,
        int order)
        where TTerminator : class, IShellTerminator
        => AddShellTerminatorCore<TTerminator>(
            services,
            phase,
            order,
            isExplicit: true,
            source: $"AddShellTerminator<{typeof(TTerminator).FullName}>");

    private static IServiceCollection AddShellTerminatorCore<TTerminator>(
        IServiceCollection services,
        LifecyclePhase phase,
        int order,
        bool isExplicit,
        string source)
        where TTerminator : class, IShellTerminator
    {
        Guard.Against.Null(services);

        var registrationIndex = services.Count(d => d.ServiceType == typeof(IShellTerminator));
        services.AddTransient<TTerminator>();
        services.AddTransient<IShellTerminator>(sp => sp.GetRequiredService<TTerminator>());
        services.AddSingleton(new ShellTerminatorRegistration(
            typeof(TTerminator),
            phase,
            order,
            registrationIndex,
            isExplicit,
            source));

        return services;
    }
}
