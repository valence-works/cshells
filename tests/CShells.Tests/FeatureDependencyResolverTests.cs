namespace CShells.Tests;

public class FeatureDependencyResolverTests
{
    private readonly FeatureDependencyResolver _resolver = new();

    [Theory]
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

    [Fact]
    public void ResolveDependencies_WithFeatureNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var features = new Dictionary<string, ShellFeatureDescriptor>();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.ResolveDependencies("NonExistent", features));
        Assert.Contains("not found", ex.Message);
        Assert.Contains("NonExistent", ex.Message);
    }

    [Fact]
    public void ResolveDependencies_WithNoDependencies_ReturnsEmptyList()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("Feature1", Array.Empty<string>())
        );

        // Act
        var result = _resolver.ResolveDependencies("Feature1", features);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveDependencies_WithSingleDependency_ReturnsDependency()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("Feature1", new[] { "Feature2" }),
            ("Feature2", Array.Empty<string>())
        );

        // Act
        var result = _resolver.ResolveDependencies("Feature1", features);

        // Assert
        Assert.Single(result);
        Assert.Equal("Feature2", result[0]);
    }

    [Fact]
    public void ResolveDependencies_WithTransitiveDependencies_ReturnsAllDependenciesInOrder()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("Feature1", new[] { "Feature2" }),
            ("Feature2", new[] { "Feature3" }),
            ("Feature3", Array.Empty<string>())
        );

        // Act
        var result = _resolver.ResolveDependencies("Feature1", features);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Feature3", result[0]);
        Assert.Equal("Feature2", result[1]);
    }

    [Fact]
    public void ResolveDependencies_WithCircularDependency_ThrowsInvalidOperationException()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("Feature1", new[] { "Feature2" }),
            ("Feature2", new[] { "Feature1" })
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.ResolveDependencies("Feature1", features));
        Assert.Contains("Circular dependency", ex.Message);
    }

    [Fact]
    public void ResolveDependencies_WithMissingDependency_ThrowsInvalidOperationException()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("Feature1", new[] { "NonExistent" })
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.ResolveDependencies("Feature1", features));
        Assert.Contains("not found", ex.Message);
        Assert.Contains("NonExistent", ex.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetOrderedFeatures_WithNullFeatures_ThrowsArgumentNullException(bool withFeatureNames)
    {
        // Act & Assert
        if (withFeatureNames)
        {
            var ex = Assert.Throws<ArgumentNullException>(() => _resolver.GetOrderedFeatures(new[] { "Feature1" }, null!));
            Assert.Equal("features", ex.ParamName);
        }
        else
        {
            var ex = Assert.Throws<ArgumentNullException>(() => _resolver.GetOrderedFeatures((IReadOnlyDictionary<string, ShellFeatureDescriptor>)null!));
            Assert.Equal("features", ex.ParamName);
        }
    }

    [Fact]
    public void GetOrderedFeatures_WithEmptyFeatures_ReturnsEmptyList()
    {
        // Arrange
        var features = new Dictionary<string, ShellFeatureDescriptor>();

        // Act
        var result = _resolver.GetOrderedFeatures(features);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetOrderedFeatures_WithNoDependencies_ReturnsAllFeatures()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("Feature1", Array.Empty<string>()),
            ("Feature2", Array.Empty<string>())
        );

        // Act
        var result = _resolver.GetOrderedFeatures(features);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("Feature1", result);
        Assert.Contains("Feature2", result);
    }

    [Fact]
    public void GetOrderedFeatures_WithDependencies_ReturnsDependenciesFirst()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("Feature1", new[] { "Feature2" }),
            ("Feature2", new[] { "Feature3" }),
            ("Feature3", Array.Empty<string>())
        );

        // Act
        var result = _resolver.GetOrderedFeatures(features);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True(result.IndexOf("Feature3") < result.IndexOf("Feature2"));
        Assert.True(result.IndexOf("Feature2") < result.IndexOf("Feature1"));
    }

    [Fact]
    public void GetOrderedFeatures_WithCircularDependency_ThrowsInvalidOperationException()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("Feature1", new[] { "Feature2" }),
            ("Feature2", new[] { "Feature3" }),
            ("Feature3", new[] { "Feature1" })
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _resolver.GetOrderedFeatures(features));
        Assert.Contains("Circular dependency", ex.Message);
    }

    [Fact]
    public void GetOrderedFeatures_WithFeatureNames_NullFeatureNames_ThrowsArgumentNullException()
    {
        // Arrange
        var features = new Dictionary<string, ShellFeatureDescriptor>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => _resolver.GetOrderedFeatures((IEnumerable<string>)null!, features));
        Assert.Equal("featureNames", ex.ParamName);
    }

    [Fact]
    public void GetOrderedFeatures_WithSelectedFeatures_ReturnsOnlySelectedFeaturesAndDependencies()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("Feature1", new[] { "Feature2" }),
            ("Feature2", Array.Empty<string>()),
            ("Feature3", Array.Empty<string>()),  // Not selected
            ("Feature4", new[] { "Feature3" })     // Not selected
        );

        // Act
        var result = _resolver.GetOrderedFeatures(new[] { "Feature1" }, features);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("Feature1", result);
        Assert.Contains("Feature2", result);
        Assert.DoesNotContain("Feature3", result);
        Assert.DoesNotContain("Feature4", result);
    }

    [Fact]
    public void GetOrderedFeatures_WithSelectedFeatures_ReturnsDependenciesFirst()
    {
        // Arrange
        var features = CreateFeatureDictionary(
            ("Feature1", new[] { "Feature2" }),
            ("Feature2", new[] { "Feature3" }),
            ("Feature3", Array.Empty<string>())
        );

        // Act
        var result = _resolver.GetOrderedFeatures(new[] { "Feature1" }, features);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True(result.IndexOf("Feature3") < result.IndexOf("Feature2"));
        Assert.True(result.IndexOf("Feature2") < result.IndexOf("Feature1"));
    }

    [Fact]
    public void GetOrderedFeatures_WithDiamondDependency_HandlesCorrectly()
    {
        // Arrange: Diamond pattern A -> B, A -> C, B -> D, C -> D
        var features = CreateFeatureDictionary(
            ("A", new[] { "B", "C" }),
            ("B", new[] { "D" }),
            ("C", new[] { "D" }),
            ("D", Array.Empty<string>())
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
