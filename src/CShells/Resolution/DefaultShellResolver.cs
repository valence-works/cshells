namespace CShells.Resolution;

/// <summary>
/// Default implementation of <see cref="IShellResolver"/> that orchestrates multiple <see cref="IShellResolverStrategy"/> instances.
/// Tries each strategy in order (sorted by <see cref="ResolverOrderAttribute"/> or registration order) and returns the first non-null result.
/// </summary>
/// <remarks>
/// Strategies are automatically sorted by their order value before execution:
/// <list type="bullet">
/// <item><description>Strategies with lower order values execute first</description></item>
/// <item><description>Order can be specified via <see cref="ResolverOrderAttribute"/> on the strategy class</description></item>
/// <item><description>Order can be overridden at registration time using <see cref="ShellResolverOptions"/></description></item>
/// <item><description>If no order is specified, the default order is 100</description></item>
/// </list>
/// </remarks>
public class DefaultShellResolver : IShellResolver
{
    private const int DefaultOrder = 100;
    private readonly IShellResolverStrategy[] _orderedStrategies;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShellResolver"/> class.
    /// </summary>
    /// <param name="strategies">The collection of strategies to evaluate. Will be sorted by order before execution.</param>
    /// <param name="options">Optional configuration options for strategy ordering.</param>
    public DefaultShellResolver(IEnumerable<IShellResolverStrategy> strategies, ShellResolverOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        // Sort strategies by order (lower values first)
        // Priority: options override > attribute > default (100)
        _orderedStrategies = strategies
            .Select(strategy => new
            {
                Strategy = strategy,
                Order = GetOrderForStrategy(strategy, options)
            })
            .OrderBy(x => x.Order)
            .Select(x => x.Strategy)
            .ToArray();
    }

    private static int GetOrderForStrategy(IShellResolverStrategy strategy, ShellResolverOptions? options)
    {
        var strategyType = strategy.GetType();

        // 1. Check if order is configured in options
        var configuredOrder = options?.GetOrder(strategyType);
        if (configuredOrder.HasValue)
            return configuredOrder.Value;

        // 2. Check if order is specified via attribute
        var attribute = strategyType.GetCustomAttributes(typeof(ResolverOrderAttribute), inherit: true)
            .OfType<ResolverOrderAttribute>()
            .FirstOrDefault();

        if (attribute != null)
            return attribute.Order;

        // 3. Use default order
        return DefaultOrder;
    }

    /// <inheritdoc />
    public ShellId? Resolve(ShellResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var strategy in _orderedStrategies)
        {
            var shellId = strategy.Resolve(context);
            if (shellId.HasValue)
            {
                return shellId;
            }
        }

        return null;
    }
}
