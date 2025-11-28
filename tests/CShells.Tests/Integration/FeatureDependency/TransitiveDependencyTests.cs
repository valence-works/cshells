using FluentAssertions;

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
        var features = CreateFeatureDictionary(
            ("A", ["B"]),
            ("B", ["C"]),
            ("C", [])
        );

        // Act
        var result = _resolver.GetOrderedFeatures(["A"], features);

        // Assert: Dependencies should come before dependents [C, B, A]
        result.Should().HaveCount(3);
        result.Should().ContainInOrder("C", "B", "A");
    }

    [Fact(DisplayName = "GetOrderedFeatures with deep transitive dependencies returns dependencies first")]
    public void GetOrderedFeatures_WithDeepTransitiveDependencies_ReturnsDependenciesBeforeDependents()
    {
        // Arrange: A -> B -> C -> D -> E
        var features = CreateFeatureDictionary(
            ("A", ["B"]),
            ("B", ["C"]),
            ("C", ["D"]),
            ("D", ["E"]),
            ("E", [])
        );

        // Act
        var result = _resolver.GetOrderedFeatures(["A"], features);

        // Assert
        result.Should().HaveCount(5);

        // Each feature should come after its dependencies
        result.IndexOf("E").Should().BeLessThan(result.IndexOf("D"));
        result.IndexOf("D").Should().BeLessThan(result.IndexOf("C"));
        result.IndexOf("C").Should().BeLessThan(result.IndexOf("B"));
        result.IndexOf("B").Should().BeLessThan(result.IndexOf("A"));
    }

    [Fact(DisplayName = "GetOrderedFeatures with multiple dependencies returns dependencies first")]
    public void GetOrderedFeatures_WithMultipleDependencies_ReturnsDependenciesFirst()
    {
        // Arrange: A depends on both B and C, which have no dependencies
        var features = CreateFeatureDictionary(
            ("A", ["B", "C"]),
            ("B", []),
            ("C", [])
        );

        // Act
        var result = _resolver.GetOrderedFeatures(["A"], features);

        // Assert
        result.Should().HaveCount(3);
        result.IndexOf("B").Should().BeLessThan(result.IndexOf("A"));
        result.IndexOf("C").Should().BeLessThan(result.IndexOf("A"));
    }

    [Fact(DisplayName = "GetOrderedFeatures with diamond dependency handles duplicates correctly")]
    public void GetOrderedFeatures_WithDiamondDependency_HandlesDuplicatesCorrectly()
    {
        // Arrange: Diamond pattern A -> B, A -> C, B -> D, C -> D
        var features = CreateFeatureDictionary(
            ("A", ["B", "C"]),
            ("B", ["D"]),
            ("C", ["D"]),
            ("D", [])
        );

        // Act
        var result = _resolver.GetOrderedFeatures(["A"], features);

        // Assert: D should appear only once and before B and C
        result.Should().HaveCount(4);
        result.Should().ContainSingle(f => f == "D");
        result.IndexOf("D").Should().BeLessThan(result.IndexOf("B"));
        result.IndexOf("D").Should().BeLessThan(result.IndexOf("C"));
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
