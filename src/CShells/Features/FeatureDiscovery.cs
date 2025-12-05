using System.Reflection;

namespace CShells.Features;

/// <summary>
/// Provides static methods for discovering shell features from assemblies.
/// </summary>
public static class FeatureDiscovery
{
    /// <summary>
    /// Discovers all features from the specified assemblies by scanning for types that implement <see cref="IShellFeature"/> or <see cref="IWebShellFeature"/>.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for features.</param>
    /// <returns>A collection of feature descriptors for all valid features found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assemblies"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when duplicate feature names are found.
    /// </exception>
    public static IEnumerable<ShellFeatureDescriptor> DiscoverFeatures(IEnumerable<Assembly> assemblies)
    {
        var assembliesList = assemblies.ToList();
        Guard.Against.Null(assembliesList);

        var features = new Dictionary<string, ShellFeatureDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assembliesList)
        {
            // Skip null assemblies
            if (assembly == null!)
                continue;

            var featureTypes = GetExportedTypes(assembly)
                .Where(type => type.IsClass && !type.IsAbstract && typeof(IShellFeature).IsAssignableFrom(type));

            foreach (var type in featureTypes)
            {
                var attribute = type.GetCustomAttribute<ShellFeatureAttribute>();
                var featureName = GetFeatureName(type, attribute);

                EnsureUniqueFeatureName(featureName, type, features);

                var descriptor = CreateFeatureDescriptor(type, attribute, featureName);
                features[featureName] = descriptor;
            }
        }

        return features.Values;
    }

    /// <summary>
    /// Gets the feature name from the attribute or derives it from the class name.
    /// </summary>
    private static string GetFeatureName(Type type, ShellFeatureAttribute? attribute)
    {
        // Use attribute name if provided
        if (attribute?.Name != null)
            return attribute.Name;

        // Derive from class name
        var className = type.Name;

        // Remove "Feature" suffix if present
        if (className.EndsWith("Feature", StringComparison.Ordinal) && className.Length > "Feature".Length)
            return className[..^"Feature".Length];

        return className;
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
    private static ShellFeatureDescriptor CreateFeatureDescriptor(Type type, ShellFeatureAttribute? attribute, string featureName)
    {
        var descriptor = new ShellFeatureDescriptor(featureName)
        {
            StartupType = type,
            Dependencies = attribute?.DependsOn ?? []
        };

        if (attribute?.Metadata is { Length: > 0 })
        {
            descriptor.Metadata = ParseMetadata(featureName, attribute.Metadata);
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
