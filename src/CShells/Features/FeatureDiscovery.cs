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
    /// <param name="onAssemblyLoadError">Optional callback invoked when an assembly fails to load its types.</param>
    /// <returns>A collection of feature descriptors for all valid features found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assemblies"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when duplicate feature names are found.
    /// </exception>
    public static IEnumerable<ShellFeatureDescriptor> DiscoverFeatures(IEnumerable<Assembly> assemblies, Action<Assembly, Exception>? onAssemblyLoadError = null)
    {
        var assembliesList = assemblies.ToList();
        Guard.Against.Null(assembliesList);

        var features = new Dictionary<string, ShellFeatureDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assembliesList)
        {
            // Skip null assemblies
            if (assembly == null!)
                continue;

            var featureTypes = GetExportedTypes(assembly, onAssemblyLoadError)
                .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IShellFeature).IsAssignableFrom(type));

            foreach (var type in featureTypes)
                AddFeatureDescriptor(type, features);
        }

        return features.Values;
    }

    private static void AddFeatureDescriptor(Type type, Dictionary<string, ShellFeatureDescriptor> features)
    {
        var pending = new Stack<Type>();
        pending.Push(type);

        while (pending.TryPop(out var currentType))
        {
            var attribute = currentType.GetCustomAttribute<ShellFeatureAttribute>();
            var featureName = GetFeatureName(currentType, attribute);
            var explicitDependencies = GetExplicitDependencies(featureName, attribute);

            if (features.TryGetValue(featureName, out var existingDescriptor))
            {
                if (existingDescriptor.StartupType == currentType)
                    continue;

                throw new InvalidOperationException(
                    $"Duplicate feature name '{featureName}' found. Type '{currentType.FullName}' conflicts with an existing feature.");
            }

            var descriptor = CreateFeatureDescriptor(currentType, attribute, featureName, explicitDependencies.Names);
            features[featureName] = descriptor;

            for (var i = explicitDependencies.Types.Count - 1; i >= 0; i--)
                pending.Push(explicitDependencies.Types[i]);
        }
    }

    /// <summary>
    /// Gets the feature name from the attribute or derives it from the class name.
    /// </summary>
    private static string GetFeatureName(Type type, ShellFeatureAttribute? attribute)
    {
        return attribute?.Name ?? StripSuffixes(type.Name, "ShellFeature", "Feature");
    }
    
    private static string StripSuffixes(string source, params string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (string.IsNullOrEmpty(suffix))
                continue;

            if (source.EndsWith(suffix, StringComparison.Ordinal) && source.Length > suffix.Length)
                return source[..^suffix.Length];
        }

        return source;
    }

    /// <summary>
    /// Gets inferred dependencies from types implementing IInfersDependenciesFrom&lt;TBaseFeature&gt;.
    /// </summary>
    private static IEnumerable<string> GetInferredDependencies(Type type)
    {
        var infersDependenciesFromInterfaces = type.GetInterfaces()
            .Where(i => i.IsGenericType &&
                        i.GetGenericTypeDefinition().FullName == "CShells.Features.IInfersDependenciesFrom`1");

        foreach (var interfaceType in infersDependenciesFromInterfaces)
        {
            var baseFeatureType = interfaceType.GetGenericArguments()[0];
            var baseFeatureAttribute = baseFeatureType.GetCustomAttribute<ShellFeatureAttribute>();
            var baseFeatureName = GetFeatureName(baseFeatureType, baseFeatureAttribute);

            yield return baseFeatureName;
        }
    }

    /// <summary>
    /// Creates a feature descriptor from a type and its ShellFeatureAttribute.
    /// </summary>
    private static ShellFeatureDescriptor CreateFeatureDescriptor(
        Type type,
        ShellFeatureAttribute? attribute,
        string featureName,
        IReadOnlyList<string> explicitDependencies)
    {
        // Get inferred dependencies from IInfersDependenciesFrom<> interface
        var inferredDependencies = GetInferredDependencies(type);

        // Combine and deduplicate dependencies
        var allDependencies = explicitDependencies.Concat(inferredDependencies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var descriptor = new ShellFeatureDescriptor(featureName)
        {
            StartupType = type,
            Dependencies = allDependencies
        };

        // Add DisplayName and Description to metadata if provided via attribute
        if (attribute != null)
        {
            if (!string.IsNullOrWhiteSpace(attribute.DisplayName))
            {
                descriptor.Metadata[ShellFeatureMetadataKeys.DisplayName] = attribute.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(attribute.Description))
            {
                descriptor.Metadata[ShellFeatureMetadataKeys.Description] = attribute.Description;
            }
        }

        // Parse additional custom metadata from the attribute
        if (attribute?.Metadata is { Length: > 0 })
        {
            var customMetadata = ParseMetadata(featureName, attribute.Metadata);
            foreach (var kvp in customMetadata)
            {
                descriptor.Metadata[kvp.Key] = kvp.Value;
            }
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

    private static IEnumerable<Type> GetExportedTypes(Assembly assembly, Action<Assembly, Exception>? onAssemblyLoadError = null)
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
        catch (TypeLoadException ex)
        {
            // A type within the assembly references something that cannot be loaded
            // (e.g. a version-mismatched dependency). Skip the assembly entirely.
            onAssemblyLoadError?.Invoke(assembly, ex);
            return [];
        }
        catch (FileNotFoundException ex)
        {
            // Assembly has missing dependencies - skip it
            onAssemblyLoadError?.Invoke(assembly, ex);
            return [];
        }
        catch (FileLoadException ex)
        {
            // Assembly cannot be loaded - skip it
            onAssemblyLoadError?.Invoke(assembly, ex);
            return [];
        }
        catch (BadImageFormatException ex)
        {
            // Assembly is not a valid .NET assembly - skip it
            onAssemblyLoadError?.Invoke(assembly, ex);
            return [];
        }
    }

    private static ExplicitDependencySet GetExplicitDependencies(string featureName, ShellFeatureAttribute? attribute)
    {
        if (attribute?.DependsOn is not { Length: > 0 } dependencies)
            return new([], []);

        var names = new List<string>(dependencies.Length);
        var types = new List<Type>(dependencies.Length);
        foreach (var dependency in dependencies)
        {
            switch (dependency)
            {
                case string name when !string.IsNullOrWhiteSpace(name):
                    names.Add(name);
                    break;
                case string:
                    throw new InvalidOperationException($"Feature '{featureName}' has an empty dependency name.");
                case Type dependencyType:
                    if (!typeof(IShellFeature).IsAssignableFrom(dependencyType))
                    {
                        throw new InvalidOperationException(
                            $"Feature '{featureName}' has dependency type '{dependencyType.FullName}' that does not implement {nameof(IShellFeature)}.");
                    }
                    if (!dependencyType.IsClass || dependencyType.IsAbstract)
                    {
                        throw new InvalidOperationException(
                            $"Feature '{featureName}' has dependency type '{dependencyType.FullName}' that is not a concrete feature type.");
                    }

                    var resolvedName = GetFeatureName(dependencyType, dependencyType.GetCustomAttribute<ShellFeatureAttribute>());
                    names.Add(resolvedName);
                    types.Add(dependencyType);
                    break;
                case null:
                    throw new InvalidOperationException($"Feature '{featureName}' has a null dependency entry.");
                default:
                    throw new InvalidOperationException(
                        $"Feature '{featureName}' has unsupported dependency type '{dependency.GetType().FullName}'. Only string and Type are supported.");
            }
        }

        return new(names, types);
    }

    private sealed record ExplicitDependencySet(IReadOnlyList<string> Names, IReadOnlyList<Type> Types);
}
