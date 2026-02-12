using Microsoft.Extensions.Configuration;

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
    /// Initializes a new instance of the <see cref="ShellBuilder"/> class.
    /// </summary>
    /// <param name="id">The shell identifier as a string.</param>
    public ShellBuilder(string id) : this(new ShellId(id))
    {
    }

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
    /// Adds a configuration data entry to the shell settings.
    /// Configuration data is used to populate the shell-scoped IConfiguration.
    /// </summary>
    public ShellBuilder WithConfigurationData(string key, object value)
    {
        Guard.Against.Null(key);
        Guard.Against.Null(value);
        _settings.ConfigurationData[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple configuration data entries to the shell settings.
    /// Configuration data is used to populate the shell-scoped IConfiguration.
    /// </summary>
    public ShellBuilder WithConfigurationData(IDictionary<string, object> configurationData)
    {
        Guard.Against.Null(configurationData);
        foreach (var (key, value) in configurationData)
            _settings.ConfigurationData[key] = value;
        return this;
    }

    /// <summary>
    /// Loads configuration from an <see cref="IConfigurationSection"/> and merges it with existing settings.
    /// Features are merged (combined), while Properties and ConfigurationData from configuration take precedence.
    /// </summary>
    /// <param name="section">The configuration section representing a shell.</param>
    /// <returns>The builder for method chaining.</returns>
    public ShellBuilder FromConfiguration(IConfigurationSection section)
    {
        Guard.Against.Null(section);

        // Load features and merge with existing
        var normalizedFeatures = ConfigurationHelper.GetNormalizedFeatures(section);

        if (normalizedFeatures.Length > 0)
        {
            var existingFeatures = _settings.EnabledFeatures.ToList();
            existingFeatures.AddRange(normalizedFeatures);
            _settings.EnabledFeatures = existingFeatures.Distinct().ToArray();
        }

        // Load properties from configuration
        var propertiesSection = section.GetSection("Properties");
        ConfigurationHelper.LoadPropertiesFromConfiguration(propertiesSection, _settings.Properties);

        // Load shell-specific settings (configuration data)
        var settingsSection = section.GetSection("Settings");
        ConfigurationHelper.LoadSettingsFromConfiguration(settingsSection, _settings.ConfigurationData);

        return this;
    }

    /// <summary>
    /// Loads configuration from a <see cref="ShellConfig"/> and merges it with existing settings.
    /// Features are merged (combined), while Properties and ConfigurationData from configuration take precedence.
    /// </summary>
    /// <param name="config">The shell configuration.</param>
    /// <returns>The builder for method chaining.</returns>
    public ShellBuilder FromConfiguration(ShellConfig config)
    {
        Guard.Against.Null(config);

        // Merge features
        var normalizedFeatures = ConfigurationHelper.NormalizeFeatures(config.Features);

        if (normalizedFeatures.Length > 0)
        {
            var existingFeatures = _settings.EnabledFeatures.ToList();
            existingFeatures.AddRange(normalizedFeatures);
            _settings.EnabledFeatures = existingFeatures.Distinct().ToArray();
        }

        // Convert and merge properties
        foreach (var property in config.Properties)
        {
            var converted = ConfigurationHelper.ConvertToJsonElement(property.Value);
            if (converted != null)
                _settings.Properties[property.Key] = converted;
        }

        // Merge settings (configuration data)
        foreach (var setting in config.Settings.Where(s => s.Value != null))
        {
            _settings.ConfigurationData[setting.Key] = setting.Value!;
        }

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
