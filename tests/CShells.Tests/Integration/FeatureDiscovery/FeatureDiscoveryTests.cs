using System.Reflection;
using CShells.Tests.TestHelpers;

namespace CShells.Tests.Integration.FeatureDiscovery;

public class FeatureDiscoveryTests
{
    [Fact(DisplayName = "DiscoverFeatures with null assemblies throws ArgumentNullException")]
    public void DiscoverFeatures_WithNullAssemblies_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => CShells.FeatureDiscovery.DiscoverFeatures(null!).ToList());
        Assert.Equal("assemblies", ex.ParamName);
    }

    [Fact(DisplayName = "DiscoverFeatures with empty assemblies returns empty collection")]
    public void DiscoverFeatures_WithEmptyAssemblies_ReturnsEmptyCollection()
    {
        // Act
        var features = CShells.FeatureDiscovery.DiscoverFeatures([]);

        // Assert
        Assert.Empty(features);
    }

    [Fact(DisplayName = "DiscoverFeatures with null assembly in collection skips null")]
    public void DiscoverFeatures_WithNullAssemblyInCollection_SkipsNullAssembly()
    {
        // Arrange
        var validAssembly = TestAssemblyBuilder.CreateTestAssembly(
            ("ValidFeature", typeof(IShellFeature), [], [])
        );
        var assemblies = new Assembly?[] { null, validAssembly, null };

        // Act
        var features = CShells.FeatureDiscovery.DiscoverFeatures(assemblies!).ToList();

        // Assert
        Assert.Single(features);
        Assert.Equal("ValidFeature", features[0].Id);
    }

    [Fact(DisplayName = "DiscoverFeatures with valid feature returns feature descriptor")]
    public void DiscoverFeatures_WithValidFeature_ReturnsFeatureDescriptor()
    {
        // Arrange - use assembly with only valid features
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("ValidTestFeature", typeof(IShellFeature), [], [])
        );

        // Act
        var features = CShells.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        var feature = features.FirstOrDefault(f => f.Id == "ValidTestFeature");
        Assert.NotNull(feature);
        Assert.Equal("ValidTestFeature", feature.Id);
        Assert.NotNull(feature.StartupType);
    }

    [Fact(DisplayName = "DiscoverFeatures with feature having dependencies sets dependencies")]
    public void DiscoverFeatures_WithFeatureHavingDependencies_SetsDependencies()
    {
        // Arrange - use assembly with feature that has dependencies
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("FeatureWithDeps", typeof(IShellFeature), ["Dependency1", "Dependency2"], [])
        );

        // Act
        var features = CShells.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        var feature = features.FirstOrDefault(f => f.Id == "FeatureWithDeps");
        Assert.NotNull(feature);
        Assert.Equal(["Dependency1", "Dependency2"], feature.Dependencies);
    }

    [Fact(DisplayName = "DiscoverFeatures with feature having metadata sets metadata")]
    public void DiscoverFeatures_WithFeatureHavingMetadata_SetsMetadata()
    {
        // Arrange - use assembly with feature that has metadata
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("FeatureWithMeta", typeof(IShellFeature), [], ["key1", "value1", "key2", "value2"])
        );

        // Act
        var features = CShells.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        var feature = features.FirstOrDefault(f => f.Id == "FeatureWithMeta");
        Assert.NotNull(feature);
        Assert.Equal("value1", feature.Metadata["key1"]);
        Assert.Equal("value2", feature.Metadata["key2"]);
    }

    [Fact(DisplayName = "DiscoverFeatures with type missing IShellStartup throws InvalidOperationException")]
    public void DiscoverFeatures_WithTypeMissingIShellStartup_ThrowsInvalidOperationException()
    {
        // Arrange - create assembly with a type that has ShellFeature but doesn't implement IShellStartup
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("InvalidFeature", null, [], [])
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => CShells.FeatureDiscovery.DiscoverFeatures([assembly]).ToList());
        Assert.Contains("does not implement IShellStartup", ex.Message);
    }

    [Fact(DisplayName = "DiscoverFeatures with duplicate feature names throws InvalidOperationException")]
    public void DiscoverFeatures_WithDuplicateFeatureNames_ThrowsInvalidOperationException()
    {
        // Arrange - create assembly with two features having the same name
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("DuplicateFeatureName", typeof(IShellFeature), [], []),
            ("DuplicateFeatureName", typeof(IShellFeature), [], [])
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => CShells.FeatureDiscovery.DiscoverFeatures([assembly]).ToList());
        Assert.Contains("Duplicate feature name", ex.Message);
        Assert.Contains("DuplicateFeatureName", ex.Message);
    }

    [Fact(DisplayName = "DiscoverFeatures with multiple valid features returns all features")]
    public void DiscoverFeatures_WithMultipleValidFeatures_ReturnsAllFeatures()
    {
        // Arrange
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("Feature1", typeof(IShellFeature), [], []),
            ("Feature2", typeof(IShellFeature), ["Feature1"], [])
        );

        // Act
        var features = CShells.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        Assert.Equal(2, features.Count);
        Assert.Contains(features, f => f.Id == "Feature1");
        Assert.Contains(features, f => f.Id == "Feature2");
    }

    [Fact(DisplayName = "DiscoverFeatures with odd metadata elements throws InvalidOperationException")]
    public void DiscoverFeatures_WithOddMetadataElements_ThrowsInvalidOperationException()
    {
        // Arrange - create assembly with odd number of metadata elements
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("FeatureWithOddMetadata", typeof(IShellFeature), [], ["key1", "value1", "orphanKey"])
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => CShells.FeatureDiscovery.DiscoverFeatures([assembly]).ToList());
        Assert.Contains("odd number of metadata elements", ex.Message);
        Assert.Contains("FeatureWithOddMetadata", ex.Message);
    }
}
