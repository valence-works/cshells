using CShells.Tests.TestHelpers;

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
        var featureList = FeatureTestHelpers.ParseFeatureDependencies(featureDependencies);
        var features = FeatureTestHelpers.CreateFeatureDictionary(featureList);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.GetOrderedFeatures(["A"], features));
        Assert.Contains(missingFeature, ex.Message);
        Assert.Contains("not found", ex.Message);
    }

    [Fact(DisplayName = "ResolveDependencies with unknown feature throws InvalidOperationException")]
    public void ResolveDependencies_WithUnknownFeature_ThrowsInvalidOperationException()
    {
        // Arrange
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("A", [])
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.ResolveDependencies("NonExistentFeature", features));
        Assert.Contains("NonExistentFeature", ex.Message);
        Assert.Contains("not found", ex.Message);
    }

}
