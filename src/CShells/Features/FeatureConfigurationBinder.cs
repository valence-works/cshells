using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CShells.Features.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Features;

/// <summary>
/// Provides configuration binding and validation for shell features.
/// </summary>
public class FeatureConfigurationBinder
{
    private readonly IConfiguration _configuration;
    private readonly IFeatureConfigurationValidator _validator;
    private readonly ILogger<FeatureConfigurationBinder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureConfigurationBinder"/> class.
    /// </summary>
    /// <param name="configuration">The configuration source.</param>
    /// <param name="validator">The validator for feature configuration.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public FeatureConfigurationBinder(
        IConfiguration configuration,
        IFeatureConfigurationValidator validator,
        ILogger<FeatureConfigurationBinder>? logger = null)
    {
        _configuration = Guard.Against.Null(configuration);
        _validator = Guard.Against.Null(validator);
        _logger = logger ?? NullLogger<FeatureConfigurationBinder>.Instance;
    }

    /// <summary>
    /// Binds and validates configuration for a feature, then calls Configure methods if the feature implements IConfigurableFeature.
    /// </summary>
    /// <param name="feature">The feature to configure.</param>
    /// <param name="featureName">The feature name used for configuration section lookup.</param>
    public void BindAndConfigure(IShellFeature feature, string featureName)
    {
        Guard.Against.Null(feature);
        Guard.Against.Null(featureName);

        // Step 1: Auto-bind properties on the feature itself
        AutoBindFeatureProperties(feature, featureName);

        // Step 2: Find and invoke all IConfigurableFeature<T> implementations
        BindConfigurableFeatureInterfaces(feature, featureName);
    }

    /// <summary>
    /// Automatically binds public settable properties on the feature from configuration.
    /// </summary>
    private void AutoBindFeatureProperties(IShellFeature feature, string featureName)
    {
        var featureType = feature.GetType();
        var configSection = _configuration.GetSection(featureName);

        if (!configSection.Exists())
        {
            _logger.LogDebug("No configuration section found for feature '{FeatureName}'", featureName);
            return;
        }

        var properties = featureType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetSetMethod()?.IsPublic == true);

        foreach (var property in properties)
        {
            BindProperty(feature, property, configSection, featureName);
        }

        // Validate the feature after auto-binding
        ValidateFeature(feature, featureName);
    }

    /// <summary>
    /// Binds a single property from configuration.
    /// </summary>
    private void BindProperty(IShellFeature feature, PropertyInfo property, IConfigurationSection configSection, string featureName)
    {
        var propertySection = configSection.GetSection(property.Name);
        if (!propertySection.Exists())
        {
            return;
        }

        try
        {
            // Handle complex types (bind entire section)
            if (IsComplexType(property.PropertyType))
            {
                var value = Activator.CreateInstance(property.PropertyType);
                if (value != null)
                {
                    propertySection.Bind(value);
                    property.SetValue(feature, value);
                    _logger.LogDebug("Bound complex property '{PropertyName}' on feature '{FeatureName}'",
                        property.Name, featureName);
                }
            }
            // Handle simple types (get value directly)
            else
            {
                var value = propertySection.Get(property.PropertyType);
                if (value != null)
                {
                    property.SetValue(feature, value);
                    _logger.LogDebug("Bound property '{PropertyName}' on feature '{FeatureName}' to value: {Value}",
                        property.Name, featureName, value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to bind property '{PropertyName}' on feature '{FeatureName}'",
                property.Name, featureName);
        }
    }

    /// <summary>
    /// Determines if a type is complex and requires section binding.
    /// </summary>
    private static bool IsComplexType(Type type)
    {
        // Primitive types, strings, and value types with TypeConverter are simple
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type.IsEnum || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
            type == typeof(TimeSpan) || type == typeof(Guid) || type == typeof(Uri))
        {
            return false;
        }

        // Nullable versions of simple types are also simple
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            return IsComplexType(underlyingType);
        }

        // Everything else is complex
        return true;
    }

    /// <summary>
    /// Finds all IConfigurableFeature&lt;T&gt; implementations and invokes their Configure methods.
    /// </summary>
    private void BindConfigurableFeatureInterfaces(IShellFeature feature, string featureName)
    {
        var featureType = feature.GetType();
        var configurableInterfaces = featureType.GetInterfaces()
            .Where(i => i.IsGenericType &&
                       i.GetGenericTypeDefinition() == typeof(IConfigurableFeature<>));

        foreach (var interfaceType in configurableInterfaces)
        {
            var optionsType = interfaceType.GetGenericArguments()[0];
            BindAndInvokeConfigure(feature, featureName, interfaceType, optionsType);
        }
    }

    /// <summary>
    /// Binds options from configuration and invokes the Configure method.
    /// </summary>
    private void BindAndInvokeConfigure(IShellFeature feature, string featureName, Type interfaceType, Type optionsType)
    {
        // Determine configuration section name
        // Try: 1. FeatureName, 2. OptionsTypeName (minus "Options" suffix)
        var sectionNames = new[]
        {
            featureName,
            GetOptionsTypeName(optionsType)
        };

        IConfigurationSection? optionsSection = null;
        foreach (var sectionName in sectionNames)
        {
            var section = _configuration.GetSection(sectionName);
            if (section.Exists())
            {
                optionsSection = section;
                break;
            }
        }

        if (optionsSection == null)
        {
            _logger.LogDebug("No configuration section found for options type '{OptionsType}' on feature '{FeatureName}'",
                optionsType.Name, featureName);
            return;
        }

        // Create and bind options instance
        var options = Activator.CreateInstance(optionsType);
        if (options == null)
        {
            _logger.LogWarning("Failed to create instance of options type '{OptionsType}' for feature '{FeatureName}'",
                optionsType.Name, featureName);
            return;
        }

        optionsSection.Bind(options);

        // Validate options
        _validator.Validate(options, $"{featureName}:{optionsType.Name}");

        // Invoke Configure method
        var configureMethod = interfaceType.GetMethod(nameof(IConfigurableFeature<object>.Configure));
        if (configureMethod != null)
        {
            try
            {
                configureMethod.Invoke(feature, new[] { options });
                _logger.LogDebug("Invoked Configure method on feature '{FeatureName}' with options type '{OptionsType}'",
                    featureName, optionsType.Name);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to configure feature '{featureName}' with options type '{optionsType.Name}': {ex.InnerException?.Message ?? ex.Message}",
                    ex.InnerException ?? ex);
            }
        }
    }

    /// <summary>
    /// Validates a feature using the configured validator.
    /// </summary>
    private void ValidateFeature(object target, string contextName)
    {
        _validator.Validate(target, contextName);
    }

    /// <summary>
    /// Gets the configuration section name for an options type.
    /// Removes "Options" suffix if present.
    /// </summary>
    private static string GetOptionsTypeName(Type optionsType)
    {
        var name = optionsType.Name;
        const string optionsSuffix = "Options";

        if (name.EndsWith(optionsSuffix) && name.Length > optionsSuffix.Length)
        {
            return name.Substring(0, name.Length - optionsSuffix.Length);
        }

        return name;
    }
}
