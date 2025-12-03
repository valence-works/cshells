namespace CShells.Resolution;

/// <summary>
/// A builder for configuring shell resolution strategies in a protocol-agnostic way.
/// </summary>
public class ShellResolutionBuilder
{
    private readonly List<IShellResolverStrategy> _strategies = [];
    private readonly Dictionary<string, object> _properties = [];
    private readonly List<Action<ShellResolutionBuilder>> _finalizers = [];

    public ShellResolutionBuilder AddStrategy(IShellResolverStrategy strategy)
    {
        _strategies.Add(Guard.Against.Null(strategy));
        return this;
    }

    public ShellResolutionBuilder AddStrategy(Func<IShellResolverStrategy> strategyFactory)
    {
        _strategies.Add(Guard.Against.Null(strategyFactory)());
        return this;
    }

    public ShellResolutionBuilder AddFinalizer(Action<ShellResolutionBuilder> finalizer)
    {
        _finalizers.Add(Guard.Against.Null(finalizer));
        return this;
    }

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

    public bool RemoveProperty(string key) => _properties.Remove(Guard.Against.NullOrEmpty(key));

    public IReadOnlyList<IShellResolverStrategy> GetStrategies()
    {
        foreach (var finalizer in _finalizers)
            finalizer(this);

        _finalizers.Clear();
        return _strategies.AsReadOnly();
    }
}
