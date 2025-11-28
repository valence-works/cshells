using FluentAssertions;

namespace CShells.Tests.Core;

/// <summary>
/// Tests for the <see cref="FeatureDependencyResolver"/> class covering transitive dependencies,
/// cycle detection, and handling of unknown feature dependencies.
/// </summary>
public class FeatureDependencyTests
{
    private readonly FeatureDependencyResolver _resolver = new();

    #region Transitive Dependency Resolution

    [Fact]
    public void GetOrderedFeatures_WithTransitiveDependencies_ReturnsTopologicalOrder()
    {
        // Arrange: A -> B -> C (A depends on B, B depends on C)
        var features = CreateFeatureDictionary(
            ("A", new[] { "B" }),
            ("B", new[] { "C" }),
            ("C", Array.Empty<string>())
        );

        // Act
        var result = _resolver.GetOrderedFeatures(new[] { "A" }, features);

        // Assert: Dependencies should come before dependents [C, B, A]
        result.Should().HaveCount(3);
        result.Should().ContainInOrder("C", "B", "A");
    }

    [Fact]
    public void GetOrderedFeatures_WithDeepTransitiveDependencies_ReturnsDependenciesBeforeDependents()
    {
        // Arrange: A -> B -> C -> D -> E
        var features = CreateFeatureDictionary(
            ("A", new[] { "B" }),
            ("B", new[] { "C" }),
            ("C", new[] { "D" }),
            ("D", new[] { "E" }),
            ("E", Array.Empty<string>())
        );

        // Act
        var result = _resolver.GetOrderedFeatures(new[] { "A" }, features);

        // Assert
        result.Should().HaveCount(5);
        
        // Each feature should come after its dependencies
        result.IndexOf("E").Should().BeLessThan(result.IndexOf("D"));
        result.IndexOf("D").Should().BeLessThan(result.IndexOf("C"));
        result.IndexOf("C").Should().BeLessThan(result.IndexOf("B"));
        result.IndexOf("B").Should().BeLessThan(result.IndexOf("A"));
    }

    [Fact]
    public void GetOrderedFeatures_WithMultipleDependencies_ReturnsDependenciesFirst()
    {
        // Arrange: A depends on both B and C, which have no dependencies
        var features = CreateFeatureDictionary(
            ("A", new[] { "B", "C" }),
            ("B", Array.Empty<string>()),
            ("C", Array.Empty<string>())
        );

        // Act
        var result = _resolver.GetOrderedFeatures(new[] { "A" }, features);

        // Assert
        result.Should().HaveCount(3);
        result.IndexOf("B").Should().BeLessThan(result.IndexOf("A"));
        result.IndexOf("C").Should().BeLessThan(result.IndexOf("A"));
    }

    [Fact]
    public void GetOrderedFeatures_WithDiamondDependency_HandlesDuplicatesCorrectly()
    {
        // Arrange: Diamond pattern A -> B, A -> C, B -> D, C -> D
        var features = CreateFeatureDictionary(
            ("A", new[] { "B", "C" }),
            ("B", new[] { "D" }),
            ("C", new[] { "D" }),
            ("D", Array.Empty<string>())
        );

        // Act
        var result = _resolver.GetOrderedFeatures(new[] { "A" }, features);

        // Assert: D should appear only once and before B and C
        result.Should().HaveCount(4);
        result.Should().ContainSingle(f => f == "D");
        result.IndexOf("D").Should().BeLessThan(result.IndexOf("B"));
        result.IndexOf("D").Should().BeLessThan(result.IndexOf("C"));
    }

    #endregion

    #region Cycle Detection

    [Theory]
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
                : Array.Empty<string>();
            return (name, deps);
        }).ToArray();

        var features = CreateFeatureDictionary(featureList);

        // Act & Assert
        var act = () => _resolver.GetOrderedFeatures(new[] { "A" }, features);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Circular dependency*", $"scenario '{scenario}' should detect circular dependency");
    }

    [Fact]
    public void ResolveDependencies_WithCycle_ThrowsInvalidOperationExceptionWithFeatureName()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("A", new[] { "B" }),
            ("B", new[] { "A" })
        );

        // Act & Assert
        var act = () => _resolver.ResolveDependencies("A", features);
        var exception = act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Circular dependency*")
            .Which;
        (exception.Message.Contains("A") || exception.Message.Contains("B")).Should().BeTrue(
            "exception message should contain the feature name involved in the cycle");
    }

    #endregion

    #region Unknown Feature Dependency Handling

    [Theory]
    [InlineData("DirectDependency", "NonExistent", "A:NonExistent")]
    [InlineData("TransitiveDependency", "NonExistent", "A:B", "B:NonExistent")]
    [InlineData("MissingFeatureName", "MissingFeature", "A:MissingFeature")]
    public void GetOrderedFeatures_WithUnknownDependency_ThrowsWithFeatureName(string scenario, string missingFeature, params string[] featureDependencies)
    {
        // Arrange
        var featureList = featureDependencies.Select(fd =>
        {
            var parts = fd.Split(':');
            return (parts[0], parts.Length > 1 ? parts[1].Split(',') : Array.Empty<string>());
        }).ToArray();

        var features = CreateFeatureDictionary(featureList);

        // Act & Assert
        var act = () => _resolver.GetOrderedFeatures(new[] { "A" }, features);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{missingFeature}*", $"scenario '{scenario}' should include missing feature name in error")
            .WithMessage("*not found*");
    }

    [Fact]
    public void ResolveDependencies_WithUnknownFeature_ThrowsInvalidOperationException()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("A", Array.Empty<string>())
        );

        // Act & Assert
        var act = () => _resolver.ResolveDependencies("NonExistentFeature", features);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*NonExistentFeature*")
            .WithMessage("*not found*");
    }

    #endregion

    #region Helper Methods

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
            dict[name] = new ShellFeatureDescriptor(name) { Dependencies = dependencies };
        }
        return dict;
    }

    #endregion
}
