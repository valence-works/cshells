namespace CShells.Features;

/// <summary>
/// Resolves feature dependencies and computes an ordered list of features using topological sort.
/// </summary>
public class FeatureDependencyResolver
{
    /// <summary>
    /// Resolves all dependencies for a given feature name, including transitive dependencies.
    /// </summary>
    /// <param name="featureName">The name of the feature to resolve dependencies for.</param>
    /// <param name="features">A dictionary mapping feature names to their descriptors.</param>
    /// <returns>A list of feature names representing all dependencies in dependency order (dependencies first).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="featureName"/> or <paramref name="features"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a circular dependency is detected or a dependency is not found.</exception>
    public List<string> ResolveDependencies(string featureName, IReadOnlyDictionary<string, ShellFeatureDescriptor> features)
    {
        Guard.Against.Null(featureName);
        Guard.Against.Null(features);

        var result = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ResolveDependenciesRecursive(featureName, null, features, visited, visiting, result);

        // Remove the original feature from the result (only return dependencies)
        result.Remove(featureName);
        return result;
    }

    /// <summary>
    /// Returns a topologically sorted list of all features, ensuring dependencies come before dependents.
    /// </summary>
    /// <param name="features">A dictionary mapping feature names to their descriptors.</param>
    /// <returns>A list of feature names in topological order (dependencies first).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="features"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a circular dependency is detected.</exception>
    public List<string> GetOrderedFeatures(IReadOnlyDictionary<string, ShellFeatureDescriptor> features)
    {
        Guard.Against.Null(features);

        var result = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var featureName in features.Keys)
        {
            ResolveDependenciesRecursive(featureName, null, features, visited, visiting, result);
        }

        return result;
    }

    /// <summary>
    /// Returns a topologically sorted list of the specified features and their dependencies.
    /// </summary>
    /// <param name="featureNames">The names of the features to sort.</param>
    /// <param name="features">A dictionary mapping feature names to their descriptors.</param>
    /// <returns>A list of feature names in topological order (dependencies first), including all transitive dependencies.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="featureNames"/> or <paramref name="features"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a circular dependency is detected or a dependency is not found.</exception>
    public List<string> GetOrderedFeatures(IEnumerable<string> featureNames, IReadOnlyDictionary<string, ShellFeatureDescriptor> features)
    {
        Guard.Against.Null(featureNames);
        Guard.Against.Null(features);

        var result = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var featureName in featureNames)
        {
            ResolveDependenciesRecursive(featureName, null, features, visited, visiting, result);
        }

        return result;
    }

    /// <summary>
    /// Performs a depth-first topological sort to resolve feature dependencies.
    /// Uses three-color algorithm: white (unvisited), gray (visiting), black (visited).
    /// </summary>
    /// <remarks>
    /// Algorithm:
    /// 1. Skip if already processed (black/visited)
    /// 2. Detect cycles by checking if currently being processed (gray/visiting)
    /// 3. Mark as being processed (gray)
    /// 4. Recursively process all dependencies first (depth-first)
    /// 5. Mark as fully processed (black) and add to result
    ///
    /// Result order: dependencies appear before their dependents (topological order).
    /// </remarks>
    private static void ResolveDependenciesRecursive(
        string featureName,
        ShellFeatureDescriptor? dependentFeature,
        IReadOnlyDictionary<string, ShellFeatureDescriptor> features,
        HashSet<string> visited,
        HashSet<string> visiting,
        List<string> result)
    {
        // Already fully processed - skip
        if (visited.Contains(featureName))
            return;

        // Currently being processed in this call stack - circular dependency!
        if (visiting.Contains(featureName))
        {
            throw new InvalidOperationException(
                $"Circular dependency detected involving feature '{featureName}'.");
        }

        // Validate feature exists
        if (!features.TryGetValue(featureName, out var descriptor))
        {
            var errorMessage = dependentFeature != null
                ? $"Feature '{featureName}' not found in the feature dictionary. Required by feature '{dependentFeature.Id}'."
                : $"Feature '{featureName}' not found in the feature dictionary.";

            throw new InvalidOperationException(errorMessage);
        }

        // Mark as being processed (gray)
        visiting.Add(featureName);

        // Process all dependencies first (depth-first traversal)
        foreach (var dependency in descriptor.Dependencies)
        {
            ResolveDependenciesRecursive(dependency, descriptor, features, visited, visiting, result);
        }

        // Done processing this feature - mark as complete (black) and add to result
        visiting.Remove(featureName);
        visited.Add(featureName);
        result.Add(featureName);
    }
}
