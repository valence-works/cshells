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
    /// Adds a feature with settings to the shell.
    /// </summary>
    /// <param name="featureId">The feature identifier.</param>
    /// <param name="configure">Action to configure the feature settings.</param>
    /// <returns>This builder for method chaining.</returns>
    public ShellBuilder WithFeature(string featureId, Action<FeatureSettingsBuilder> configure)
    {
        Guard.Against.Null(featureId);
        Guard.Against.Null(configure);

        // Add the feature to enabled features
        var currentFeatures = _settings.EnabledFeatures.ToList();
        if (!currentFeatures.Contains(featureId))
            currentFeatures.Add(featureId);
        _settings.EnabledFeatures = [..currentFeatures];

        // Build the settings
        var settingsBuilder = new FeatureSettingsBuilder(featureId);
        configure(settingsBuilder);
        settingsBuilder.ApplyTo(_settings.ConfigurationData);

        return this;
    }

    /// <summary>
    /// Adds a feature entry (with optional settings) to the shell.
    /// </summary>
    public ShellBuilder WithFeature(FeatureEntry feature)
    {
        Guard.Against.Null(feature);

        var currentFeatures = _settings.EnabledFeatures.ToList();
        if (!currentFeatures.Contains(feature.Name))
            currentFeatures.Add(feature.Name);
        _settings.EnabledFeatures = [..currentFeatures];

        // Apply feature settings to configuration data
        ConfigurationHelper.PopulateFeatureSettings([feature], _settings.ConfigurationData);

        return this;
    }

    /// <summary>
    /// Adds a configuration entry to the shell settings.
    /// Configuration data is used to populate the shell-scoped IConfiguration.
    /// </summary>
    public ShellBuilder WithConfiguration(string key, object value)
    {
        Guard.Against.Null(key);
        Guard.Against.Null(value);
        _settings.ConfigurationData[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple configuration entries to the shell settings.
    /// Configuration data is used to populate the shell-scoped IConfiguration.
    /// </summary>
    public ShellBuilder WithConfiguration(IDictionary<string, object> configuration)
    {
        Guard.Against.Null(configuration);
        foreach (var (key, value) in configuration)
            _settings.ConfigurationData[key] = value;
        return this;
    }

    /// <summary>
    /// Loads configuration from an <see cref="IConfigurationSection"/> and merges it with existing settings.
    /// Features are merged (combined), while Configuration from the section takes precedence.
    /// </summary>
    /// <param name="section">The configuration section representing a shell.</param>
    /// <returns>The builder for method chaining.</returns>
    public ShellBuilder FromConfiguration(IConfigurationSection section)
    {
        Guard.Against.Null(section);

        // Parse features from configuration (handles mixed string/object array)
        var featuresSection = section.GetSection("Features");
        var features = ConfigurationHelper.ParseFeaturesFromConfiguration(featuresSection);

        if (features.Count > 0)
        {
            var existingFeatures = _settings.EnabledFeatures.ToList();
            var newFeatureNames = ConfigurationHelper.ExtractFeatureNames(features);
            existingFeatures.AddRange(newFeatureNames);
            _settings.EnabledFeatures = existingFeatures.Distinct().ToArray();

            // Apply feature settings
            ConfigurationHelper.PopulateFeatureSettings(features, _settings.ConfigurationData);
        }

        // Load shell-level configuration
        var configurationSection = section.GetSection("Configuration");
        ConfigurationHelper.LoadConfigurationFromSection(configurationSection, _settings.ConfigurationData);

        return this;
    }

    /// <summary>
    /// Loads configuration from a <see cref="ShellConfig"/> and merges it with existing settings.
    /// Features are merged (combined), while Configuration takes precedence.
    /// </summary>
    /// <param name="config">The shell configuration.</param>
    /// <returns>The builder for method chaining.</returns>
    public ShellBuilder FromConfiguration(ShellConfig config)
    {
        Guard.Against.Null(config);

        // Merge features
        var featureNames = ConfigurationHelper.ExtractFeatureNames(config.Features);

        if (featureNames.Length > 0)
        {
            var existingFeatures = _settings.EnabledFeatures.ToList();
            existingFeatures.AddRange(featureNames);
            _settings.EnabledFeatures = existingFeatures.Distinct().ToArray();
        }

        // Apply feature settings
        ConfigurationHelper.PopulateFeatureSettings(config.Features, _settings.ConfigurationData);

        // Apply shell-level configuration
        ConfigurationHelper.PopulateShellConfiguration(config.Configuration, _settings.ConfigurationData);

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

/// <summary>
/// Builder for feature-specific settings.
/// </summary>
public class FeatureSettingsBuilder
{
    private readonly string _featureName;
    private readonly Dictionary<string, object> _settings = new();

    internal FeatureSettingsBuilder(string featureName)
    {
        _featureName = featureName;
    }

    /// <summary>
    /// Adds a setting for the feature.
    /// </summary>
    public FeatureSettingsBuilder WithSetting(string key, object value)
    {
        Guard.Against.Null(key);
        Guard.Against.Null(value);
        _settings[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple settings for the feature.
    /// </summary>
    public FeatureSettingsBuilder WithSettings(IDictionary<string, object> settings)
    {
        Guard.Against.Null(settings);
        foreach (var (key, value) in settings)
            _settings[key] = value;
        return this;
    }

    internal void ApplyTo(IDictionary<string, object> configurationData)
    {
        foreach (var (key, value) in _settings)
        {
            configurationData[$"{_featureName}:{key}"] = value;
        }
    }
}

