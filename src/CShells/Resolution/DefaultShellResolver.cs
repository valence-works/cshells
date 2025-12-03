namespace CShells.Resolution;

/// <summary>
/// Default implementation of <see cref="IShellResolver"/> that orchestrates multiple <see cref="IShellResolverStrategy"/> instances.
/// </summary>
public class DefaultShellResolver : IShellResolver
{
    private const int DefaultOrder = 100;

    private readonly IShellResolverStrategy[] _orderedStrategies;

    public DefaultShellResolver(IEnumerable<IShellResolverStrategy> strategies, ShellResolverOptions? options = null)
    {
        if (strategies is null)
            throw new ArgumentNullException(nameof(strategies));
        _orderedStrategies = strategies
            .OrderBy(s => GetOrderForStrategy(s, options))
            .ToArray();
    }

    /// <inheritdoc />
    public ShellId? Resolve(ShellResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var strategy in _orderedStrategies)
        {
            var shellId = strategy.Resolve(context);
            if (shellId.HasValue)
                return shellId;
        }

        return null;
    }
    
    private static int GetOrderForStrategy(IShellResolverStrategy strategy, ShellResolverOptions? options)
    {
        var strategyType = strategy.GetType();

        var configuredOrder = options?.GetOrder(strategyType);
        if (configuredOrder.HasValue)
            return configuredOrder.Value;

        var attribute = strategyType.GetCustomAttributes(typeof(ResolverOrderAttribute), inherit: true)
            .OfType<ResolverOrderAttribute>()
            .FirstOrDefault();

        return attribute?.Order ?? DefaultOrder;
    }
}
