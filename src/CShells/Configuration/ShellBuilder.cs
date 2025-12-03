namespace CShells.Configuration;

/// <summary>
/// Fluent API for building shell configurations.
/// </summary>
public class ShellBuilder
{
    private readonly ShellSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellBuilder"/> class.
    /// </summary>
    /// <param name="id">The shell identifier.</param>
    public ShellBuilder(ShellId id)
    {
        _settings = new ShellSettings(id);
    }

    /// <summary>
    /// Adds features to the shell.
    /// </summary>
    /// <param name="featureIds">The feature identifiers to enable.</param>
    /// <returns>This builder for method chaining.</returns>
    public ShellBuilder WithFeatures(params string[] featureIds)
    {
        Guard.Against.Null(featureIds);
        _settings.EnabledFeatures = featureIds.ToArray();
        return this;
    }

    /// <summary>
    /// Adds a single feature to the shell.
    /// </summary>
    /// <param name="featureId">The feature identifier to enable.</param>
    /// <returns>This builder for method chaining.</returns>
    public ShellBuilder WithFeature(string featureId)
    {
        Guard.Against.Null(featureId);
        var currentFeatures = _settings.EnabledFeatures.ToList();
        currentFeatures.Add(featureId);
        _settings.EnabledFeatures = currentFeatures.ToArray();
        return this;
    }

    /// <summary>
    /// Adds a property to the shell settings.
    /// </summary>
    /// <param name="key">The property key.</param>
    /// <param name="value">The property value.</param>
    /// <returns>This builder for method chaining.</returns>
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
    /// <param name="properties">The properties to add.</param>
    /// <returns>This builder for method chaining.</returns>
    public ShellBuilder WithProperties(IDictionary<string, object> properties)
    {
        Guard.Against.Null(properties);
        foreach (var (key, value) in properties)
        {
            _settings.Properties[key] = value;
        }
        return this;
    }

    /// <summary>
    /// Builds the shell settings.
    /// </summary>
    /// <returns>The configured shell settings.</returns>
    public ShellSettings Build()
    {
        return _settings;
    }

    /// <summary>
    /// Implicitly converts a builder to shell settings.
    /// </summary>
    public static implicit operator ShellSettings(ShellBuilder builder) => builder.Build();
}
