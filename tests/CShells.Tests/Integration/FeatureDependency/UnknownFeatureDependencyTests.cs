using CShells.Features;
using CShells.Tests.TestHelpers;

namespace CShells.Tests.Integration.FeatureDependency;

/// <summary>
/// Tests for unknown feature dependency handling in <see cref="FeatureDependencyResolver"/>.
/// </summary>
public class UnknownFeatureDependencyTests
{
    private readonly FeatureDependencyResolver _resolver = new();

    [Theory(DisplayName = "GetOrderedFeatures with unknown dependency throws with feature name")]
    [MemberData(nameof(FeatureDependencyData.UnknownDependencyCases), MemberType = typeof(FeatureDependencyData))]
    public void GetOrderedFeatures_WithUnknownDependency_ThrowsWithFeatureName(IEnumerable<string> roots, string missingFeature, string[] dependencyMap)
    {
        // Arrange
        var featureList = FeatureTestHelpers.ParseFeatureDependencies(dependencyMap);
        var features = FeatureTestHelpers.CreateFeatureDictionary(featureList);

        // Act & Assert
        var ex = Assert.Throws<FeatureNotFoundException>(() => _resolver.GetOrderedFeatures(roots, features));
        Assert.Contains(missingFeature, ex.Message);
        Assert.Contains("not found", ex.Message);
    }

    [Theory(DisplayName = "ResolveDependencies with unknown feature throws FeatureNotFoundException")]
    [MemberData(nameof(FeatureDependencyData.UnknownDependencyCases), MemberType = typeof(FeatureDependencyData))]
    public void ResolveDependencies_WithUnknownFeature_ThrowsFeatureNotFoundException(IEnumerable<string> _, string missingFeature, string[] dependencyMap)
    {
        // Arrange
        var featureList = FeatureTestHelpers.ParseFeatureDependencies(dependencyMap);
        var features = FeatureTestHelpers.CreateFeatureDictionary(featureList);

        // Act & Assert
        var ex = Assert.Throws<FeatureNotFoundException>(() => _resolver.ResolveDependencies(missingFeature, features));
        Assert.Contains(missingFeature, ex.Message);
        Assert.Contains("not found", ex.Message);
    }
}
