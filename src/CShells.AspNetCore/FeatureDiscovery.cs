using System.Reflection;
using CShells;

namespace CShells.AspNetCore;

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
    /// Thrown when a type decorated with <see cref="ShellFeatureAttribute"/> does not implement <see cref="IShellStartup"/>,
    /// or when duplicate feature names are found.
    /// </exception>
    public static IEnumerable<ShellFeatureDescriptor> DiscoverFeatures(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var features = new Dictionary<string, ShellFeatureDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assemblies.Where(a => a != null))
        {
            foreach (var type in GetExportedTypes(assembly))
            {
                var attribute = type.GetCustomAttribute<ShellFeatureAttribute>();
                if (attribute == null)
                    continue;

                // Validate that the type implements IShellStartup
                if (!typeof(IShellStartup).IsAssignableFrom(type))
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' is decorated with [ShellFeature(\"{attribute.Name}\")] but does not implement IShellStartup.");
                }

                // Check for duplicate feature names
                if (features.ContainsKey(attribute.Name))
                {
                    throw new InvalidOperationException(
                        $"Duplicate feature name '{attribute.Name}' found. Type '{type.FullName}' conflicts with an existing feature.");
                }

                var descriptor = new ShellFeatureDescriptor(attribute.Name)
                {
                    StartupType = type,
                    Dependencies = attribute.DependsOn
                };

                // Convert metadata array to dictionary (pairs of key-value)
                if (attribute.Metadata.Length > 0)
                {
                    if (attribute.Metadata.Length % 2 != 0)
                    {
                        throw new InvalidOperationException(
                            $"Feature '{attribute.Name}' has an odd number of metadata elements. Metadata must be specified as key-value pairs.");
                    }
                    
                    var metadata = new Dictionary<string, object>();
                    for (var i = 0; i + 1 < attribute.Metadata.Length; i += 2)
                    {
                        var key = attribute.Metadata[i]?.ToString();
                        if (!string.IsNullOrEmpty(key))
                        {
                            metadata[key] = attribute.Metadata[i + 1];
                        }
                    }
                    descriptor.Metadata = metadata;
                }

                features[attribute.Name] = descriptor;
            }
        }

        return features.Values;
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
