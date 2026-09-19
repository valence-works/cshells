using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CShells.Lifecycle;

/// <summary>
/// Extension methods for registering shell lifecycle components.
/// </summary>
public static class ServiceCollectionLifecycleExtensions
{
    /// <summary>
    /// Registers a shell initializer in <see cref="LifecyclePhase.Default"/> with order <c>0</c>.
    /// </summary>
    /// <typeparam name="TInitializer">The initializer implementation type.</typeparam>
    /// <param name="services">The shell service collection to register into.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This is the first-class equivalent of registering a transient
    /// <see cref="IShellInitializer"/> directly, with default-phase lifecycle metadata attached.
    /// <typeparamref name="TInitializer"/> is only registered as transient if the consumer has
    /// not already registered it; an existing lifetime is preserved.
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
    /// Registers a shell initializer in <see cref="LifecyclePhase.Default"/>.
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
    /// Registers a shell initializer in the specified lifecycle phase.
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
    /// <typeparamref name="TInitializer"/> is registered as transient only if it is not
    /// already registered, so an explicit lifetime chosen by the consumer — for example
    /// <c>services.AddSingleton&lt;MyInitializer&gt;()</c> — is preserved, and the lifecycle
    /// then initializes that same instance. It is exposed as <see cref="IShellInitializer"/>
    /// without replacing any existing descriptors.
    /// <code>
    /// services.AddShellInitializer&lt;ApplyMigrationsInitializer&gt;(LifecyclePhase.Prepare, order: 100);
    /// services.AddShellInitializer&lt;StartSchedulerInitializer&gt;(LifecyclePhase.Start, order: 100);
    /// </code>
    /// As an alternative for legacy registrations, apply
    /// <see cref="LifecycleOrderAttribute"/> to the initializer implementation type.
    /// <para>
    /// <b>Ordering contract.</b> Initializers are sorted ascending by <paramref name="phase"/>,
    /// then by <paramref name="order"/>, then by the position of the initializer in the
    /// resolved <see cref="IShellInitializer"/> sequence. A <em>lower</em>
    /// <paramref name="order"/> therefore runs <em>first</em>, and negative values are
    /// perfectly valid — they are the idiomatic way to run ahead of a component you do not
    /// own. Terminators are the exact mirror: see
    /// <see cref="AddShellTerminator{TTerminator}(IServiceCollection, LifecyclePhase, int)"/>.
    /// </para>
    /// <para>
    /// Only <paramref name="phase"/> is validated. There is no constraint whatsoever on
    /// <paramref name="order"/>: any <see cref="int"/> is accepted, and no range is reserved.
    /// </para>
    /// <para>
    /// Two initializers sharing the same <paramref name="phase"/> and <paramref name="order"/>
    /// is <b>not</b> an error. Activation proceeds, the tie is broken deterministically by
    /// DI registration order, and a non-fatal diagnostic is logged naming the tied types.
    /// The diagnostic is only emitted when at least one of the tied initializers was ordered
    /// explicitly through this API; ties between attribute-ordered or unordered initializers
    /// are silent.
    /// </para>
    /// <para>
    /// <see cref="ShellInitializerOrderException"/> — despite what a tie might suggest — is
    /// reserved for metadata that cannot produce a plan at all: a <paramref name="phase"/>
    /// value undefined in <see cref="LifecyclePhase"/>, ordering metadata whose type does not
    /// implement <see cref="IShellInitializer"/>, or ordering metadata registered for a type
    /// that DI never resolved.
    /// </para>
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
        services.TryAddTransient<TInitializer>();
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
    /// Registers a shell terminator in <see cref="LifecyclePhase.Default"/> with order <c>0</c>.
    /// </summary>
    /// <typeparam name="TTerminator">The terminator implementation type.</typeparam>
    /// <param name="services">The shell service collection to register into.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This is the first-class equivalent of registering a transient
    /// <see cref="IShellTerminator"/> directly, with default-phase lifecycle metadata attached.
    /// <typeparamref name="TTerminator"/> is only registered as transient if the consumer has
    /// not already registered it; an existing lifetime is preserved.
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
    /// Registers a shell terminator in <see cref="LifecyclePhase.Default"/>.
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
    /// Registers a shell terminator in the specified lifecycle phase.
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
    /// transient only if it is not already registered, so an explicit lifetime chosen by the
    /// consumer — for example <c>services.AddSingleton&lt;MyTerminator&gt;()</c> — is preserved,
    /// and the lifecycle then terminates that same instance. It is exposed as
    /// <see cref="IShellTerminator"/> without replacing any existing descriptors.
    /// <code>
    /// services.AddShellInitializer&lt;StartSchedulerInitializer&gt;(LifecyclePhase.Start, order: 100);
    /// services.AddShellTerminator&lt;StopSchedulerTerminator&gt;(LifecyclePhase.Start, order: 100);
    /// </code>
    /// As an alternative for legacy registrations, apply
    /// <see cref="LifecycleOrderAttribute"/> to the terminator implementation type.
    /// <para>
    /// <b>Ordering contract.</b> Termination is the exact mirror of initialization: terminators
    /// are sorted <em>descending</em> by <paramref name="phase"/>, then by
    /// <paramref name="order"/>, then by the position of the terminator in the resolved
    /// <see cref="IShellTerminator"/> sequence. A <em>higher</em> <paramref name="order"/>
    /// therefore tears down <em>first</em>, which is what makes a terminator registered at the
    /// same phase and order as its initializer unwind at the mirrored point. Negative values
    /// are perfectly valid.
    /// </para>
    /// <para>
    /// Only <paramref name="phase"/> is validated. There is no constraint whatsoever on
    /// <paramref name="order"/>: any <see cref="int"/> is accepted, and no range is reserved.
    /// </para>
    /// <para>
    /// Two terminators sharing the same <paramref name="phase"/> and <paramref name="order"/>
    /// is <b>not</b> an error. Drain proceeds, the tie is broken deterministically by reverse
    /// DI registration order, and a non-fatal diagnostic is logged naming the tied types. The
    /// diagnostic is only emitted when at least one of the tied terminators was ordered
    /// explicitly through this API; ties between attribute-ordered or unordered terminators
    /// are silent.
    /// </para>
    /// <para>
    /// <see cref="ShellTerminatorOrderException"/> — despite what a tie might suggest — is
    /// reserved for metadata that cannot produce a plan at all: a <paramref name="phase"/>
    /// value undefined in <see cref="LifecyclePhase"/>, ordering metadata whose type does not
    /// implement <see cref="IShellTerminator"/>, or ordering metadata registered for a type
    /// that DI never resolved.
    /// </para>
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
        services.TryAddTransient<TTerminator>();
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
