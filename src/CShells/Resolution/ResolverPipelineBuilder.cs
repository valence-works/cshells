using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CShells.Resolution;

/// <summary>
/// A fluent builder for configuring the shell resolver strategy pipeline.
/// Provides explicit control over which resolver strategies are registered and their execution order.
/// </summary>
public class ResolverPipelineBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<StrategyRegistration> _registrations = new();
    private bool _isConfigured;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResolverPipelineBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    internal ResolverPipelineBuilder(IServiceCollection services)
    {
        _services = Guard.Against.Null(services);
    }

    /// <summary>
    /// Gets a value indicating whether the pipeline has been explicitly configured.
    /// </summary>
    internal bool IsConfigured => _isConfigured;

    /// <summary>
    /// Adds a resolver strategy to the pipeline.
    /// </summary>
    /// <typeparam name="TStrategy">The type of the resolver strategy.</typeparam>
    /// <param name="order">Optional execution order. If not specified, uses the order from <see cref="ResolverOrderAttribute"/> or 100 as default.</param>
    /// <returns>The builder for method chaining.</returns>
    public ResolverPipelineBuilder Use<TStrategy>(int? order = null)
        where TStrategy : class, IShellResolverStrategy
    {
        _isConfigured = true;
        _registrations.Add(new StrategyRegistration(typeof(TStrategy), order, null));
        return this;
    }

    /// <summary>
    /// Adds a resolver strategy instance to the pipeline.
    /// </summary>
    /// <param name="strategy">The resolver strategy instance.</param>
    /// <param name="order">Optional execution order. If not specified, uses the order from <see cref="ResolverOrderAttribute"/> or 100 as default.</param>
    /// <returns>The builder for method chaining.</returns>
    public ResolverPipelineBuilder Use(IShellResolverStrategy strategy, int? order = null)
    {
        Guard.Against.Null(strategy);
        _isConfigured = true;
        _registrations.Add(new StrategyRegistration(strategy.GetType(), order, strategy));
        return this;
    }

    /// <summary>
    /// Adds a fallback resolver strategy to the pipeline with a high execution order (1000).
    /// </summary>
    /// <typeparam name="TStrategy">The type of the fallback resolver strategy.</typeparam>
    /// <returns>The builder for method chaining.</returns>
    public ResolverPipelineBuilder UseFallback<TStrategy>()
        where TStrategy : class, IShellResolverStrategy
    {
        return Use<TStrategy>(order: 1000);
    }

    /// <summary>
    /// Clears all registered strategies from the pipeline.
    /// </summary>
    /// <returns>The builder for method chaining.</returns>
    public ResolverPipelineBuilder Clear()
    {
        _isConfigured = true;
        _registrations.Clear();
        return this;
    }

    /// <summary>
    /// Applies the configured pipeline to the service collection.
    /// </summary>
    internal void Build()
    {
        if (!_isConfigured)
            return;

        // Register marker to indicate pipeline was explicitly configured
        _services.TryAddSingleton<ResolverPipelineConfigurationMarker>();

        // Remove any existing resolver strategy registrations
        var existingStrategies = _services
            .Where(d => d.ServiceType == typeof(IShellResolverStrategy))
            .ToList();

        foreach (var descriptor in existingStrategies)
        {
            _services.Remove(descriptor);
        }

        // Register configured strategies
        foreach (var registration in _registrations)
        {
            if (registration.Instance != null)
            {
                _services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellResolverStrategy>(registration.Instance));
            }
            else
            {
                _services.TryAddEnumerable(
                    ServiceDescriptor.Singleton(typeof(IShellResolverStrategy), registration.StrategyType));
            }

            // Configure order if specified
            if (registration.Order.HasValue)
            {
                _services.Configure<ShellResolverOptions>(opt =>
                    opt.SetOrder(registration.StrategyType, registration.Order.Value));
            }
        }
    }

    private record StrategyRegistration(Type StrategyType, int? Order, IShellResolverStrategy? Instance);
}
