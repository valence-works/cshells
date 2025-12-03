namespace CShells.Configuration;

/// <summary>
/// Fluent API for building shell configurations.
/// </summary>
public class ShellBuilder(ShellId id)
{
    private readonly ShellSettings _settings = new(id);

    /// <summary>
    /// Adds features to the shell.
    /// </summary>
    public ShellBuilder WithFeatures(params string[] featureIds)
    {
        Guard.Against.Null(featureIds);
        _settings.EnabledFeatures = [..featureIds];
        return this;
    }

    /// <summary>
    /// Adds a single feature to the shell.
    /// </summary>
    public ShellBuilder WithFeature(string featureId)
    {
        Guard.Against.Null(featureId);
        var currentFeatures = _settings.EnabledFeatures.ToList();
        currentFeatures.Add(featureId);
        _settings.EnabledFeatures = [..currentFeatures];
        return this;
    }

    /// <summary>
    /// Adds a property to the shell settings.
    /// </summary>
    public ShellBuilder WithProperty(string key, object value)
    {
        Guard.Against.Null(key);
        Guard.Against.Null(value);
        _settings.Properties[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple properties to the shell settings.
    /// </summary>
    public ShellBuilder WithProperties(IDictionary<string, object> properties)
    {
        Guard.Against.Null(properties);
        foreach (var (key, value) in properties)
            _settings.Properties[key] = value;
        return this;
    }

    /// <summary>
    /// Builds the shell settings.
    /// </summary>
    public ShellSettings Build() => _settings;

    /// <summary>
    /// Implicitly converts a builder to shell settings.
    /// </summary>
    public static implicit operator ShellSettings(ShellBuilder builder) => builder.Build();
}
