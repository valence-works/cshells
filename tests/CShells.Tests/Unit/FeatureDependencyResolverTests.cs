using CShells.Features;
using CShells.Tests.TestHelpers;

namespace CShells.Tests.Unit;

public class FeatureDependencyResolverTests
{
    private readonly FeatureDependencyResolver _resolver = new();

    [Theory(DisplayName = "ResolveDependencies with null parameters throws ArgumentNullException")]
    [InlineData(null, "featureName")]
    [InlineData("Feature1", "features")]
    public void ResolveDependencies_WithNullParameters_ThrowsArgumentNullException(string? featureName, string expectedParamName)
    {
        // Arrange
        var features = featureName == null ? new Dictionary<string, ShellFeatureDescriptor>() : null;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => _resolver.ResolveDependencies(featureName!, features!));
        Assert.Equal(expectedParamName, ex.ParamName);
    }

    [Fact(DisplayName = "ResolveDependencies with unknown feature throws FeatureNotFoundException")]
    public void ResolveDependencies_WithFeatureNotFound_ThrowsFeatureNotFoundException()
    {
        // Arrange
        var features = new Dictionary<string, ShellFeatureDescriptor>();

        // Act & Assert
        var ex = Assert.Throws<FeatureNotFoundException>(() => _resolver.ResolveDependencies("NonExistent", features));
        Assert.Contains("not found", ex.Message);
        Assert.Contains("NonExistent", ex.Message);
    }

    [Fact(DisplayName = "ResolveDependencies with no dependencies returns empty list")]
    public void ResolveDependencies_WithNoDependencies_ReturnsEmptyList()
    {
        // Arrange
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("Feature1", [])
        );

        // Act
        var result = _resolver.ResolveDependencies("Feature1", features);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "ResolveDependencies with single dependency returns dependency")]
    public void ResolveDependencies_WithSingleDependency_ReturnsDependency()
    {
        // Arrange
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("Feature1", ["Feature2"]),
            ("Feature2", [])
        );

        // Act
        var result = _resolver.ResolveDependencies("Feature1", features);

        // Assert
        Assert.Single(result);
        Assert.Equal("Feature2", result[0]);
    }

    [Fact(DisplayName = "ResolveDependencies with transitive dependencies returns all in order")]
    public void ResolveDependencies_WithTransitiveDependencies_ReturnsAllDependenciesInOrder()
    {
        // Arrange
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("Feature1", ["Feature2"]),
            ("Feature2", ["Feature3"]),
            ("Feature3", [])
        );

        // Act
        var result = _resolver.ResolveDependencies("Feature1", features);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Feature3", result[0]);
        Assert.Equal("Feature2", result[1]);
    }

    [Fact(DisplayName = "ResolveDependencies with circular dependency throws InvalidOperationException")]
    public void ResolveDependencies_WithCircularDependency_ThrowsInvalidOperationException()
    {
        // Arrange
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("Feature1", ["Feature2"]),
            ("Feature2", ["Feature1"])
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.ResolveDependencies("Feature1", features));
        Assert.Contains("Circular dependency", ex.Message);
    }

    [Fact(DisplayName = "ResolveDependencies with missing dependency throws FeatureNotFoundException")]
    public void ResolveDependencies_WithMissingDependency_ThrowsFeatureNotFoundException()
    {
        // Arrange
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("Feature1", ["NonExistent"])
        );

        // Act & Assert
        var ex = Assert.Throws<FeatureNotFoundException>(() => _resolver.ResolveDependencies("Feature1", features));
        Assert.Contains("not found", ex.Message);
        Assert.Contains("NonExistent", ex.Message);
    }

    [Theory(DisplayName = "GetOrderedFeatures with null features throws ArgumentNullException")]
    [InlineData(true)]
    [InlineData(false)]
    public void GetOrderedFeatures_WithNullFeatures_ThrowsArgumentNullException(bool withFeatureNames)
    {
        // Act & Assert
        if (withFeatureNames)
        {
            var ex = Assert.Throws<ArgumentNullException>(() => _resolver.GetOrderedFeatures(["Feature1"], null!));
            Assert.Equal("features", ex.ParamName);
        }
        else
        {
            var ex = Assert.Throws<ArgumentNullException>(() => _resolver.GetOrderedFeatures((IReadOnlyDictionary<string, ShellFeatureDescriptor>)null!));
            Assert.Equal("features", ex.ParamName);
        }
    }

    [Fact(DisplayName = "GetOrderedFeatures with empty features returns empty list")]
    public void GetOrderedFeatures_WithEmptyFeatures_ReturnsEmptyList()
    {
        // Arrange
        var features = new Dictionary<string, ShellFeatureDescriptor>();

        // Act
        var result = _resolver.GetOrderedFeatures(features);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "GetOrderedFeatures with no dependencies returns all features")]
    public void GetOrderedFeatures_WithNoDependencies_ReturnsAllFeatures()
    {
        // Arrange
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("Feature1", []),
            ("Feature2", [])
        );

        // Act
        var result = _resolver.GetOrderedFeatures(features);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("Feature1", result);
        Assert.Contains("Feature2", result);
    }

    [Fact(DisplayName = "GetOrderedFeatures with dependencies returns dependencies first")]
    public void GetOrderedFeatures_WithDependencies_ReturnsDependenciesFirst()
    {
        // Arrange
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("Feature1", ["Feature2"]),
            ("Feature2", ["Feature3"]),
            ("Feature3", [])
        );

        // Act
        var result = _resolver.GetOrderedFeatures(features);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True(result.IndexOf("Feature3") < result.IndexOf("Feature2"));
        Assert.True(result.IndexOf("Feature2") < result.IndexOf("Feature1"));
    }

    [Fact(DisplayName = "GetOrderedFeatures with circular dependency throws InvalidOperationException")]
    public void GetOrderedFeatures_WithCircularDependency_ThrowsInvalidOperationException()
    {
        // Arrange
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("Feature1", ["Feature2"]),
            ("Feature2", ["Feature3"]),
            ("Feature3", ["Feature1"])
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.GetOrderedFeatures(features));
        Assert.Contains("Circular dependency", ex.Message);
    }

    [Fact(DisplayName = "GetOrderedFeatures with null feature names throws ArgumentNullException")]
    public void GetOrderedFeatures_WithFeatureNames_NullFeatureNames_ThrowsArgumentNullException()
    {
        // Arrange
        var features = new Dictionary<string, ShellFeatureDescriptor>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => _resolver.GetOrderedFeatures((IEnumerable<string>)null!, features));
        Assert.Equal("featureNames", ex.ParamName);
    }

    [Fact(DisplayName = "GetOrderedFeatures with selected features returns only those and dependencies")]
    public void GetOrderedFeatures_WithSelectedFeatures_ReturnsOnlySelectedFeaturesAndDependencies()
    {
        // Arrange
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("Feature1", ["Feature2"]),
            ("Feature2", []),
            ("Feature3", []),  // Not selected
            ("Feature4", ["Feature3"])     // Not selected
        );

        // Act
        var result = _resolver.GetOrderedFeatures(["Feature1"], features);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("Feature1", result);
        Assert.Contains("Feature2", result);
        Assert.DoesNotContain("Feature3", result);
        Assert.DoesNotContain("Feature4", result);
    }

    [Fact(DisplayName = "GetOrderedFeatures with selected features returns dependencies first")]
    public void GetOrderedFeatures_WithSelectedFeatures_ReturnsDependenciesFirst()
    {
        // Arrange
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("Feature1", ["Feature2"]),
            ("Feature2", ["Feature3"]),
            ("Feature3", [])
        );

        // Act
        var result = _resolver.GetOrderedFeatures(["Feature1"], features);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True(result.IndexOf("Feature3") < result.IndexOf("Feature2"));
        Assert.True(result.IndexOf("Feature2") < result.IndexOf("Feature1"));
    }

    [Fact(DisplayName = "GetOrderedFeatures with diamond dependency handles correctly")]
    public void GetOrderedFeatures_WithDiamondDependency_HandlesCorrectly()
    {
        // Arrange: Diamond pattern A -> B, A -> C, B -> D, C -> D
        var features = FeatureTestHelpers.CreateFeatureDictionary(
            ("A", ["B", "C"]),
            ("B", ["D"]),
            ("C", ["D"]),
            ("D", [])
        );

        // Act
        var result = _resolver.GetOrderedFeatures(features);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.True(result.IndexOf("D") < result.IndexOf("B"));
        Assert.True(result.IndexOf("D") < result.IndexOf("C"));
        Assert.True(result.IndexOf("B") < result.IndexOf("A"));
        Assert.True(result.IndexOf("C") < result.IndexOf("A"));
    }

}
