namespace CShells.Tests.Integration.FeatureDependency;

/// <summary>
/// Tests for unknown feature dependency handling in <see cref="FeatureDependencyResolver"/>.
/// </summary>
public class UnknownFeatureDependencyTests
{
    private readonly FeatureDependencyResolver _resolver = new();

    [Theory(DisplayName = "GetOrderedFeatures with unknown dependency throws with feature name")]
    [InlineData("DirectDependency", "NonExistent", "A:NonExistent")]
    [InlineData("TransitiveDependency", "NonExistent", "A:B", "B:NonExistent")]
    [InlineData("MissingFeatureName", "MissingFeature", "A:MissingFeature")]
    public void GetOrderedFeatures_WithUnknownDependency_ThrowsWithFeatureName(string scenario, string missingFeature, params string[] featureDependencies)
    {
        // Arrange
        var featureList = featureDependencies.Select(fd =>
        {
            var parts = fd.Split(':');
            return (parts[0], parts.Length > 1 ? parts[1].Split(',') : []);
        }).ToArray();

        var features = CreateFeatureDictionary(featureList);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.GetOrderedFeatures(["A"], features));
        Assert.Contains(missingFeature, ex.Message);
        Assert.Contains("not found", ex.Message);
    }

    [Fact(DisplayName = "ResolveDependencies with unknown feature throws InvalidOperationException")]
    public void ResolveDependencies_WithUnknownFeature_ThrowsInvalidOperationException()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("A", [])
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.ResolveDependencies("NonExistentFeature", features));
        Assert.Contains("NonExistentFeature", ex.Message);
        Assert.Contains("not found", ex.Message);
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
