namespace CShells.Resolution;

/// <summary>
/// A builder for configuring shell resolution strategies in a protocol-agnostic way.
/// </summary>
public class ShellResolutionBuilder
{
    private readonly List<IShellResolverStrategy> _strategies = [];
    private readonly Dictionary<string, object> _properties = [];
    private readonly List<Action<ShellResolutionBuilder>> _finalizers = [];

    /// <summary>
    /// Adds a custom shell resolver strategy to the resolution pipeline.
    /// </summary>
    /// <param name="strategy">The strategy to add.</param>
    /// <returns>The builder for method chaining.</returns>
    public ShellResolutionBuilder AddStrategy(IShellResolverStrategy strategy)
    {
        _strategies.Add(Guard.Against.Null(strategy));
        return this;
    }

    /// <summary>
    /// Adds a custom shell resolver strategy to the resolution pipeline using a factory function.
    /// </summary>
    /// <param name="strategyFactory">A function that creates the strategy.</param>
    /// <returns>The builder for method chaining.</returns>
    public ShellResolutionBuilder AddStrategy(Func<IShellResolverStrategy> strategyFactory)
    {
        _strategies.Add(Guard.Against.Null(strategyFactory)());
        return this;
    }

    /// <summary>
    /// Adds a finalizer that will be invoked before building the resolver.
    /// Finalizers allow extension methods to perform cleanup or add resolvers based on accumulated state.
    /// </summary>
    /// <param name="finalizer">The finalizer action.</param>
    /// <returns>The builder for method chaining.</returns>
    public ShellResolutionBuilder AddFinalizer(Action<ShellResolutionBuilder> finalizer)
    {
        _finalizers.Add(Guard.Against.Null(finalizer));
        return this;
    }

    /// <summary>
    /// Gets or creates a property value for use by extension methods.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="key">The property key.</param>
    /// <param name="factory">A factory function to create the value if it doesn't exist.</param>
    /// <returns>The property value.</returns>
    public T GetOrCreateProperty<T>(string key, Func<T> factory) where T : notnull
    {
        key = Guard.Against.NullOrEmpty(key);
        var creator = Guard.Against.Null(factory);

        if (!_properties.TryGetValue(key, out var value))
        {
            value = creator();
            _properties[key] = value;
        }

        return (T)value;
    }

    /// <summary>
    /// Tries to get a property value.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="key">The property key.</param>
    /// <param name="value">The property value if found.</param>
    /// <returns><c>true</c> if the property exists; otherwise, <c>false</c>.</returns>
    public bool TryGetProperty<T>(string key, out T? value)
    {
        key = Guard.Against.NullOrEmpty(key);

        if (_properties.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Removes a property from the builder.
    /// </summary>
    /// <param name="key">The property key.</param>
    /// <returns><c>true</c> if the property was removed; otherwise, <c>false</c>.</returns>
    public bool RemoveProperty(string key)
    {
        return _properties.Remove(Guard.Against.NullOrEmpty(key));
    }

    /// <summary>
    /// Gets all configured strategies.
    /// </summary>
    /// <returns>A read-only collection of all configured strategies.</returns>
    public IReadOnlyList<IShellResolverStrategy> GetStrategies()
    {
        // Run all finalizers before returning strategies
        foreach (var finalizer in _finalizers)
        {
            finalizer(this);
        }

        // Clear finalizers so they don't run again
        _finalizers.Clear();

        return _strategies.AsReadOnly();
    }
}
