using System.Reflection;

namespace CShells;

/// <summary>
/// Provides static methods for discovering shell features from assemblies.
/// </summary>
public static class FeatureDiscovery
{
    /// <summary>
    /// Discovers all features from the specified assemblies by scanning for types decorated with <see cref="ShellFeatureAttribute"/>.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for features.</param>
    /// <returns>A collection of feature descriptors for all valid features found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assemblies"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a type decorated with <see cref="ShellFeatureAttribute"/> does not implement <see cref="IShellFeature"/>,
    /// or when duplicate feature names are found.
    /// </exception>
    public static IEnumerable<ShellFeatureDescriptor> DiscoverFeatures(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var features = new Dictionary<string, ShellFeatureDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assemblies)
        {
            // Skip null assemblies
            if (assembly == null)
                continue;

            foreach (var type in GetExportedTypes(assembly))
            {
                var attribute = type.GetCustomAttribute<ShellFeatureAttribute>();
                if (attribute == null)
                    continue;

                ValidateFeatureType(type, attribute);
                EnsureUniqueFeatureName(attribute.Name, type, features);

                var descriptor = CreateFeatureDescriptor(type, attribute);
                features[attribute.Name] = descriptor;
            }
        }

        return features.Values;
    }

    /// <summary>
    /// Validates that a type decorated with ShellFeatureAttribute implements IShellFeature.
    /// </summary>
    private static void ValidateFeatureType(Type type, ShellFeatureAttribute attribute)
    {
        if (!typeof(IShellFeature).IsAssignableFrom(type))
        {
            throw new InvalidOperationException(
                $"Type '{type.FullName}' is decorated with [ShellFeature(\"{attribute.Name}\")] but does not implement IShellStartup.");
        }
    }

    /// <summary>
    /// Ensures that a feature name is unique within the collection.
    /// </summary>
    private static void EnsureUniqueFeatureName(string featureName, Type type, Dictionary<string, ShellFeatureDescriptor> features)
    {
        if (features.ContainsKey(featureName))
        {
            throw new InvalidOperationException(
                $"Duplicate feature name '{featureName}' found. Type '{type.FullName}' conflicts with an existing feature.");
        }
    }

    /// <summary>
    /// Creates a feature descriptor from a type and its ShellFeatureAttribute.
    /// </summary>
    private static ShellFeatureDescriptor CreateFeatureDescriptor(Type type, ShellFeatureAttribute attribute)
    {
        var descriptor = new ShellFeatureDescriptor(attribute.Name)
        {
            StartupType = type,
            Dependencies = attribute.DependsOn
        };

        if (attribute.Metadata.Length > 0)
        {
            descriptor.Metadata = ParseMetadata(attribute.Name, attribute.Metadata);
        }

        return descriptor;
    }

    /// <summary>
    /// Parses metadata from an array of key-value pairs into a dictionary.
    /// </summary>
    private static Dictionary<string, object> ParseMetadata(string featureName, object[] metadataArray)
    {
        if (metadataArray.Length % 2 != 0)
        {
            throw new InvalidOperationException(
                $"Feature '{featureName}' has an odd number of metadata elements. Metadata must be specified as key-value pairs.");
        }

        var metadata = new Dictionary<string, object>();
        for (var i = 0; i + 1 < metadataArray.Length; i += 2)
        {
            var key = metadataArray[i]?.ToString();
            if (!string.IsNullOrEmpty(key))
            {
                metadata[key] = metadataArray[i + 1];
            }
        }

        return metadata;
    }

    private static IEnumerable<Type> GetExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (NotSupportedException)
        {
            // GetExportedTypes() is not supported on dynamic assemblies
            // Fall back to GetTypes() and filter for public types
            return assembly.GetTypes().Where(t => t.IsPublic);
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Return the types that were successfully loaded
            return ex.Types.OfType<Type>();
        }
    }
}
