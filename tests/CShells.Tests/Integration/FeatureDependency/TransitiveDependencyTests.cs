using CShells.Tests.TestHelpers;

namespace CShells.Tests.Integration.FeatureDependency;

/// <summary>
/// Tests for transitive dependency resolution in <see cref="FeatureDependencyResolver"/>.
/// </summary>
public class TransitiveDependencyTests
{
    private readonly FeatureDependencyResolver _resolver = new();

    [Fact(DisplayName = "GetOrderedFeatures with transitive dependencies returns topological order")]
    public void GetOrderedFeatures_WithTransitiveDependencies_ReturnsTopologicalOrder()
    {
        // Arrange: A -> B -> C (A depends on B, B depends on C)
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("A", ["B"]),
            ("B", ["C"]),
            ("C", [])
        );

        // Act
        var result = _resolver.GetOrderedFeatures(["A"], features);

        // Assert: Dependencies should come before dependents [C, B, A]
        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { "C", "B", "A" }, result);
    }

    [Fact(DisplayName = "GetOrderedFeatures with deep transitive dependencies returns dependencies first")]
    public void GetOrderedFeatures_WithDeepTransitiveDependencies_ReturnsDependenciesBeforeDependents()
    {
        // Arrange: A -> B -> C -> D -> E
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("A", ["B"]),
            ("B", ["C"]),
            ("C", ["D"]),
            ("D", ["E"]),
            ("E", [])
        );

        // Act
        var result = _resolver.GetOrderedFeatures(["A"], features);

        // Assert
        Assert.Equal(5, result.Count);

        // Each feature should come after its dependencies
        Assert.True(result.IndexOf("E") < result.IndexOf("D"));
        Assert.True(result.IndexOf("D") < result.IndexOf("C"));
        Assert.True(result.IndexOf("C") < result.IndexOf("B"));
        Assert.True(result.IndexOf("B") < result.IndexOf("A"));
    }

    [Fact(DisplayName = "GetOrderedFeatures with multiple dependencies returns dependencies first")]
    public void GetOrderedFeatures_WithMultipleDependencies_ReturnsDependenciesFirst()
    {
        // Arrange: A depends on both B and C, which have no dependencies
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("A", ["B", "C"]),
            ("B", []),
            ("C", [])
        );

        // Act
        var result = _resolver.GetOrderedFeatures(["A"], features);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True(result.IndexOf("B") < result.IndexOf("A"));
        Assert.True(result.IndexOf("C") < result.IndexOf("A"));
    }

    [Fact(DisplayName = "GetOrderedFeatures with diamond dependency handles duplicates correctly")]
    public void GetOrderedFeatures_WithDiamondDependency_HandlesDuplicatesCorrectly()
    {
        // Arrange: Diamond pattern A -> B, A -> C, B -> D, C -> D
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("A", ["B", "C"]),
            ("B", ["D"]),
            ("C", ["D"]),
            ("D", [])
        );

        // Act
        var result = _resolver.GetOrderedFeatures(["A"], features);

        // Assert: D should appear only once and before B and C
        Assert.Equal(4, result.Count);
        Assert.Single(result.Where(f => f == "D"));
        Assert.True(result.IndexOf("D") < result.IndexOf("B"));
        Assert.True(result.IndexOf("D") < result.IndexOf("C"));
    }

}
