using Microsoft.Extensions.DependencyInjection;

namespace CShells.Features;

/// <summary>
/// Default implementation of <see cref="IShellFeatureFactory"/> that creates feature instances
/// using dependency injection with smart <see cref="ShellSettings"/> parameter handling.
/// </summary>
public class DefaultShellFeatureFactory : IShellFeatureFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShellFeatureFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve feature dependencies.</param>
    public DefaultShellFeatureFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = Guard.Against.Null(serviceProvider);
    }

    /// <inheritdoc />
    public T CreateFeature<T>(Type featureType, ShellSettings? shellSettings = null) where T : class
    {
        featureType = Guard.Against.Null(featureType);

        if (!typeof(T).IsAssignableFrom(featureType))
        {
            throw new ArgumentException(
                $"Feature type '{featureType.FullName}' does not implement '{typeof(T).FullName}'.",
                nameof(featureType));
        }

        // Check if any constructor accepts ShellSettings as a parameter
        var hasShellSettingsParameter = featureType.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Any(p => p.ParameterType == typeof(ShellSettings));

        if (hasShellSettingsParameter && shellSettings != null)
        {
            // Pass ShellSettings as explicit parameter - ActivatorUtilities will match it to the constructor parameter
            return (T)ActivatorUtilities.CreateInstance(_serviceProvider, featureType, shellSettings);
        }

        // No constructor accepts ShellSettings, or no ShellSettings provided
        return (T)ActivatorUtilities.CreateInstance(_serviceProvider, featureType);
    }
}
