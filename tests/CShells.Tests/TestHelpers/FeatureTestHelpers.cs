namespace CShells.Tests.TestHelpers;

/// <summary>
/// Helper methods for creating test feature dictionaries and related test data.
/// </summary>
public static class FeatureTestHelpers
{
    /// <summary>
    /// Creates a dictionary of <see cref="ShellFeatureDescriptor"/> instances for testing.
    /// Only populates the Id and Dependencies properties since those are what
    /// the FeatureDependencyResolver operates on.
    /// </summary>
    /// <param name="features">Tuples of (FeatureName, Dependencies)</param>
    /// <returns>A dictionary with case-insensitive string keys</returns>
    public static Dictionary<string, ShellFeatureDescriptor> CreateFeatureDictionary(
        params (string Name, string[] Dependencies)[] features)
    {
        var dict = new Dictionary<string, ShellFeatureDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, dependencies) in features)
        {
            dict[name] = new ShellFeatureDescriptor(name) { Dependencies = dependencies };
        }
        return dict;
    }

    /// <summary>
    /// Parses feature dependency strings in format "FeatureName:Dep1,Dep2" into tuples.
    /// </summary>
    /// <param name="featureDependencies">Array of strings in format "Name:Deps"</param>
    /// <returns>Array of tuples (Name, Dependencies[])</returns>
    public static (string Name, string[] Dependencies)[] ParseFeatureDependencies(params string[] featureDependencies)
    {
        return featureDependencies.Select(fd =>
        {
            var parts = fd.Split(':');
            var name = parts[0];
            var deps = parts.Length > 1 && !string.IsNullOrEmpty(parts[1])
                ? parts[1].Split(',')
                : [];
            return (name, deps);
        }).ToArray();
    }
}
