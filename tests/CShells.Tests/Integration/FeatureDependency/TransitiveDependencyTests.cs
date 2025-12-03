using CShells.Features;
using CShells.Tests.TestHelpers;

namespace CShells.Tests.Integration.FeatureDependency;

/// <summary>
/// Tests for transitive dependency resolution in <see cref="FeatureDependencyResolver"/>.
/// </summary>
public class TransitiveDependencyTests
{
    private readonly FeatureDependencyResolver _resolver = new();

    [Theory(DisplayName = "GetOrderedFeatures returns dependencies before dependents")]
    [MemberData(nameof(FeatureDependencyData.TransitiveDependencyCases), MemberType = typeof(FeatureDependencyData))]
    public void GetOrderedFeatures_OrdersDependenciesBeforeDependents(IEnumerable<string> roots, string[] expectedOrder, string[] dependencyMap)
    {
        // Arrange
        var features = FeatureTestHelpers.CreateFeatureDictionary(FeatureTestHelpers.ParseFeatureDependencies(dependencyMap));

        // Act
        var result = _resolver.GetOrderedFeatures(roots, features);

        // Assert
        Assert.Equal(expectedOrder.Length, result.Count);
        Assert.Equal(expectedOrder, result);
    }

    [Theory(DisplayName = "GetOrderedFeatures deduplicates shared dependencies")]
    [MemberData(nameof(FeatureDependencyData.DiamondDependencyCases), MemberType = typeof(FeatureDependencyData))]
    public void GetOrderedFeatures_DeduplicatesSharedDependencies(IEnumerable<string> roots, string[] expectedOrder, string[] dependencyMap)
    {
        // Arrange
        var features = FeatureTestHelpers.CreateFeatureDictionary(FeatureTestHelpers.ParseFeatureDependencies(dependencyMap));

        // Act
        var result = _resolver.GetOrderedFeatures(roots, features);

        // Assert
        Assert.Equal(expectedOrder.Length, result.Count);
        Assert.Equal(expectedOrder, result);
        Assert.Single(result, f => f == "D");
    }
}
