namespace CShells.Resolution;

/// <summary>
/// Configuration options for shell resolver strategy ordering.
/// </summary>
public class ShellResolverOptions
{
    private readonly Dictionary<Type, int> _strategyOrders = new();

    /// <summary>
    /// Sets the execution order for a specific resolver strategy type.
    /// </summary>
    /// <param name="strategyType">The type of the resolver strategy.</param>
    /// <param name="order">The execution order. Lower values execute first.</param>
    public void SetOrder(Type strategyType, int order)
    {
        ArgumentNullException.ThrowIfNull(strategyType);
        _strategyOrders[strategyType] = order;
    }

    /// <summary>
    /// Sets the execution order for a specific resolver strategy type.
    /// </summary>
    /// <typeparam name="TStrategy">The type of the resolver strategy.</typeparam>
    /// <param name="order">The execution order. Lower values execute first.</param>
    public void SetOrder<TStrategy>(int order) where TStrategy : IShellResolverStrategy
    {
        _strategyOrders[typeof(TStrategy)] = order;
    }

    /// <summary>
    /// Gets the configured order for a specific resolver strategy type.
    /// </summary>
    /// <param name="strategyType">The type of the resolver strategy.</param>
    /// <returns>The configured order, or null if no order was configured for this type.</returns>
    public int? GetOrder(Type strategyType)
    {
        ArgumentNullException.ThrowIfNull(strategyType);
        return _strategyOrders.TryGetValue(strategyType, out var order) ? order : null;
    }
}
