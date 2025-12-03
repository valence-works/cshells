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
    public T CreateFeature<T>(Type featureType, ShellSettings? shellSettings = null) where T : class
    {
        featureType = Guard.Against.Null(featureType);

        if (!typeof(T).IsAssignableFrom(featureType))
            throw new ArgumentException($"Feature type '{featureType.FullName}' does not implement '{typeof(T).FullName}'.", nameof(featureType));

        // Check if any constructor accepts ShellSettings as a parameter
        var hasShellSettingsParameter = featureType.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Any(p => p.ParameterType == typeof(ShellSettings));

        return hasShellSettingsParameter && shellSettings != null
            ? (T)ActivatorUtilities.CreateInstance(_serviceProvider, featureType, shellSettings)
            : (T)ActivatorUtilities.CreateInstance(_serviceProvider, featureType);
    }
}
