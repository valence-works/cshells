using CShells.Features;
using CShells.Tests.TestHelpers;

namespace CShells.Tests.Integration.FeatureDependency;

/// <summary>
/// Tests for circular dependency detection in <see cref="FeatureDependencyResolver"/>.
/// </summary>
public class CycleDetectionTests
{
    private readonly FeatureDependencyResolver _resolver = new();

    [Theory(DisplayName = "GetOrderedFeatures with circular dependency throws InvalidOperationException")]
    [MemberData(nameof(FeatureDependencyData.CircularDependencyCases), MemberType = typeof(FeatureDependencyData))]
    public void GetOrderedFeatures_WithCircularDependency_ThrowsInvalidOperationException(IEnumerable<string> roots, string[] dependencyMap)
    {
        // Arrange: Parse dependencies from format "Feature:Dep1,Dep2"
        var featureList = FeatureTestHelpers.ParseFeatureDependencies(dependencyMap);
        var features = FeatureTestHelpers.CreateFeatureDictionary(featureList);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.GetOrderedFeatures(roots, features));
        Assert.Contains("Circular dependency", ex.Message);
    }

    [Theory(DisplayName = "ResolveDependencies with cycle throws with feature name")]
    [MemberData(nameof(FeatureDependencyData.CircularDependencyCases), MemberType = typeof(FeatureDependencyData))]
    public void ResolveDependencies_WithCycle_ThrowsInvalidOperationExceptionWithFeatureName(IEnumerable<string> roots, string[] dependencyMap)
    {
        // Arrange
        var featureList = FeatureTestHelpers.ParseFeatureDependencies(dependencyMap);
        var features = FeatureTestHelpers.CreateFeatureDictionary(featureList);
        var target = roots.First();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.ResolveDependencies(target, features));
        Assert.Contains("Circular dependency", ex.Message);
        Assert.True(ex.Message.Contains(target, StringComparison.OrdinalIgnoreCase),
            "exception message should contain the feature name involved in the cycle");
    }
}
