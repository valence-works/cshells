namespace CShells.Tests.Integration.FeatureDependency;

/// <summary>
/// Tests for circular dependency detection in <see cref="FeatureDependencyResolver"/>.
/// </summary>
public class CycleDetectionTests
{
    private readonly FeatureDependencyResolver _resolver = new();

    [Theory(DisplayName = "GetOrderedFeatures with circular dependency throws InvalidOperationException")]
    [InlineData("DirectCycle", "A:B", "B:A")] // A -> B -> A
    [InlineData("IndirectCycle", "A:B", "B:C", "C:A")] // A -> B -> C -> A
    [InlineData("SelfReference", "A:A")] // A -> A
    public void GetOrderedFeatures_WithCircularDependency_ThrowsInvalidOperationException(string scenario, params string[] featureDependencies)
    {
        // Arrange: Parse dependencies from format "Feature:Dep1,Dep2"
        var featureList = featureDependencies.Select(fd =>
        {
            var parts = fd.Split(':');
            var name = parts[0];
            var deps = parts.Length > 1 && !string.IsNullOrEmpty(parts[1])
                ? parts[1].Split(',')
                : [];
            return (name, deps);
        }).ToArray();

        var features = CreateFeatureDictionary(featureList);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.GetOrderedFeatures(["A"], features));
        Assert.Contains("Circular dependency", ex.Message);
    }

    [Fact(DisplayName = "ResolveDependencies with cycle throws with feature name")]
    public void ResolveDependencies_WithCycle_ThrowsInvalidOperationExceptionWithFeatureName()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("A", ["B"]),
            ("B", ["A"])
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.ResolveDependencies("A", features));
        Assert.Contains("Circular dependency", ex.Message);
        Assert.True(ex.Message.Contains("A") || ex.Message.Contains("B"),
            "exception message should contain the feature name involved in the cycle");
    }

    /// <summary>
    /// Creates a minimal feature dictionary for testing dependency resolution.
    /// Only populates the Id and Dependencies properties since those are what
    /// the FeatureDependencyResolver operates on.
    /// </summary>
    private static Dictionary<string, ShellFeatureDescriptor> CreateFeatureDictionary(
        params (string Name, string[] Dependencies)[] features)
    {
        var dict = new Dictionary<string, ShellFeatureDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, dependencies) in features)
        {
            dict[name] = new(name) { Dependencies = dependencies };
        }
        return dict;
    }
}
