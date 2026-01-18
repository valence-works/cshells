using Microsoft.Extensions.DependencyInjection;

namespace CShells.Features;

/// <summary>
/// Default implementation of <see cref="IShellFeatureFactory"/> that creates feature instances
/// using dependency injection with smart <see cref="ShellSettings"/> parameter handling.
/// </summary>
public class DefaultShellFeatureFactory(IServiceProvider serviceProvider) : IShellFeatureFactory
{
    private readonly IServiceProvider _serviceProvider = Guard.Against.Null(serviceProvider);

    /// <inheritdoc />
    public T CreateFeature<T>(Type featureType, ShellSettings? shellSettings = null, ShellFeatureContext? featureContext = null) where T : class
    {
        featureType = Guard.Against.Null(featureType);

        if (!typeof(T).IsAssignableFrom(featureType))
            throw new ArgumentException($"Feature type '{featureType.FullName}' does not implement '{typeof(T).FullName}'.", nameof(featureType));

        // Check if any constructor accepts ShellFeatureContext or ShellSettings as a parameter
        var constructors = featureType.GetConstructors();
        var hasContextParameter = constructors
            .SelectMany(c => c.GetParameters())
            .Any(p => p.ParameterType == typeof(ShellFeatureContext));
        var hasSettingsParameter = constructors
            .SelectMany(c => c.GetParameters())
            .Any(p => p.ParameterType == typeof(ShellSettings));

        // Priority: ShellFeatureContext > ShellSettings > no special parameters
        // This ensures features can choose their preferred injection style
        if (hasContextParameter && featureContext != null)
        {
            return (T)ActivatorUtilities.CreateInstance(_serviceProvider, featureType, featureContext);
        }

        if (hasSettingsParameter && shellSettings != null)
        {
            return (T)ActivatorUtilities.CreateInstance(_serviceProvider, featureType, shellSettings);
        }

        return (T)ActivatorUtilities.CreateInstance(_serviceProvider, featureType);
    }
}
