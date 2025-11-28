using System.Reflection;
using CShells.Tests.TestHelpers;

namespace CShells.Tests;

public class FeatureDiscoveryTests
{
    [Fact]
    public void DiscoverFeatures_WithNullAssemblies_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => FeatureDiscovery.DiscoverFeatures(null!).ToList());
        Assert.Equal("assemblies", ex.ParamName);
    }

    [Fact]
    public void DiscoverFeatures_WithEmptyAssemblies_ReturnsEmptyCollection()
    {
        // Act
        var features = FeatureDiscovery.DiscoverFeatures([]);

        // Assert
        Assert.Empty(features);
    }

    [Fact]
    public void DiscoverFeatures_WithNullAssemblyInCollection_SkipsNullAssembly()
    {
        // Arrange
        var validAssembly = TestAssemblyBuilder.CreateTestAssembly(
            ("ValidFeature", typeof(IShellFeature), Array.Empty<string>(), Array.Empty<object>())
        );
        var assemblies = new Assembly?[] { null, validAssembly, null };

        // Act
        var features = FeatureDiscovery.DiscoverFeatures(assemblies!).ToList();

        // Assert
        Assert.Single(features);
        Assert.Equal("ValidFeature", features[0].Id);
    }

    [Fact]
    public void DiscoverFeatures_WithValidFeature_ReturnsFeatureDescriptor()
    {
        // Arrange - use assembly with only valid features
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("ValidTestFeature", typeof(IShellFeature), Array.Empty<string>(), Array.Empty<object>())
        );

        // Act
        var features = FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList();

        // Assert
        var feature = features.FirstOrDefault(f => f.Id == "ValidTestFeature");
        Assert.NotNull(feature);
        Assert.Equal("ValidTestFeature", feature.Id);
        Assert.NotNull(feature.StartupType);
    }

    [Fact]
    public void DiscoverFeatures_WithFeatureHavingDependencies_SetsDependencies()
    {
        // Arrange - use assembly with feature that has dependencies
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("FeatureWithDeps", typeof(IShellFeature), new[] { "Dependency1", "Dependency2" }, Array.Empty<object>())
        );

        // Act
        var features = FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList();

        // Assert
        var feature = features.FirstOrDefault(f => f.Id == "FeatureWithDeps");
        Assert.NotNull(feature);
        Assert.Equal(new[] { "Dependency1", "Dependency2" }, feature.Dependencies);
    }

    [Fact]
    public void DiscoverFeatures_WithFeatureHavingMetadata_SetsMetadata()
    {
        // Arrange - use assembly with feature that has metadata
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("FeatureWithMeta", typeof(IShellFeature), Array.Empty<string>(), new object[] { "key1", "value1", "key2", "value2" })
        );

        // Act
        var features = FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList();

        // Assert
        var feature = features.FirstOrDefault(f => f.Id == "FeatureWithMeta");
        Assert.NotNull(feature);
        Assert.Equal("value1", feature.Metadata["key1"]);
        Assert.Equal("value2", feature.Metadata["key2"]);
    }

    [Fact]
    public void DiscoverFeatures_WithTypeMissingIShellStartup_ThrowsInvalidOperationException()
    {
        // Arrange - create assembly with a type that has ShellFeature but doesn't implement IShellStartup
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("InvalidFeature", null, Array.Empty<string>(), Array.Empty<object>())
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList());
        Assert.Contains("does not implement IShellStartup", ex.Message);
    }

    [Fact]
    public void DiscoverFeatures_WithDuplicateFeatureNames_ThrowsInvalidOperationException()
    {
        // Arrange - create assembly with two features having the same name
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("DuplicateFeatureName", typeof(IShellFeature), Array.Empty<string>(), Array.Empty<object>()),
            ("DuplicateFeatureName", typeof(IShellFeature), Array.Empty<string>(), Array.Empty<object>())
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList());
        Assert.Contains("Duplicate feature name", ex.Message);
        Assert.Contains("DuplicateFeatureName", ex.Message);
    }

    [Fact]
    public void DiscoverFeatures_WithMultipleValidFeatures_ReturnsAllFeatures()
    {
        // Arrange
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("Feature1", typeof(IShellFeature), Array.Empty<string>(), Array.Empty<object>()),
            ("Feature2", typeof(IShellFeature), new[] { "Feature1" }, Array.Empty<object>())
        );

        // Act
        var features = FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList();

        // Assert
        Assert.Equal(2, features.Count);
        Assert.Contains(features, f => f.Id == "Feature1");
        Assert.Contains(features, f => f.Id == "Feature2");
    }

    [Fact]
    public void DiscoverFeatures_WithOddMetadataElements_ThrowsInvalidOperationException()
    {
        // Arrange - create assembly with odd number of metadata elements
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("FeatureWithOddMetadata", typeof(IShellFeature), Array.Empty<string>(), new object[] { "key1", "value1", "orphanKey" })
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList());
        Assert.Contains("odd number of metadata elements", ex.Message);
        Assert.Contains("FeatureWithOddMetadata", ex.Message);
    }
}
