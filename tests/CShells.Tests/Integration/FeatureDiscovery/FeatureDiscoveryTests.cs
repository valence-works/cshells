using System.Reflection;
using CShells.Features;
using CShells.Tests.TestHelpers;

namespace CShells.Tests.Integration.FeatureDiscovery;

public class FeatureDiscoveryTests
{
    [Fact(DisplayName = "DiscoverFeatures with null assemblies throws ArgumentNullException")]
    public void DiscoverFeatures_WithNullAssemblies_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => CShells.Features.FeatureDiscovery.DiscoverFeatures(null!).ToList());
        Assert.Equal("source", ex.ParamName);
    }

    [Fact(DisplayName = "DiscoverFeatures with empty assemblies returns empty collection")]
    public void DiscoverFeatures_WithEmptyAssemblies_ReturnsEmptyCollection()
    {
        // Act
        var features = CShells.Features.FeatureDiscovery.DiscoverFeatures([]);

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
        var features = CShells.Features.FeatureDiscovery.DiscoverFeatures(assemblies!).ToList();

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
        var features = CShells.Features.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

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
        var features = CShells.Features.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

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
        var features = CShells.Features.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        var feature = features.FirstOrDefault(f => f.Id == "FeatureWithMeta");
        Assert.NotNull(feature);
        Assert.Equal("value1", feature.Metadata["key1"]);
        Assert.Equal("value2", feature.Metadata["key2"]);
    }

    [Fact(DisplayName = "DiscoverFeatures without attribute derives name from class name with Feature suffix")]
    public void DiscoverFeatures_WithoutAttribute_DerivesNameFromClassNameWithFeatureSuffix()
    {
        // Arrange - create assembly with feature class named "PaymentFeature" without attribute
        var assembly = TestAssemblyBuilder.CreateTestAssemblyWithoutAttribute("PaymentFeature");

        // Act
        var features = CShells.Features.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        Assert.Single(features);
        Assert.Equal("Payment", features[0].Id);
    }

    [Fact(DisplayName = "DiscoverFeatures without attribute and no Feature suffix uses full class name")]
    public void DiscoverFeatures_WithoutAttributeAndNoFeatureSuffix_UsesFullClassName()
    {
        // Arrange - create assembly with feature class named "Payment" without attribute
        var assembly = TestAssemblyBuilder.CreateTestAssemblyWithoutAttribute("Payment");

        // Act
        var features = CShells.Features.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        Assert.Single(features);
        Assert.Equal("Payment", features[0].Id);
    }

    [Fact(DisplayName = "DiscoverFeatures with attribute name overrides derived name")]
    public void DiscoverFeatures_WithAttributeName_OverridesDerivedName()
    {
        // Arrange - create assembly with feature that has attribute with explicit name
        var assembly = TestAssemblyBuilder.CreateTestAssembly(
            ("ExplicitName", typeof(IShellFeature), [], [])
        );

        // Act
        var features = CShells.Features.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        Assert.Single(features);
        Assert.Equal("ExplicitName", features[0].Id);
    }

    [Fact(DisplayName = "DiscoverFeatures without attribute for multiple features returns all with derived names")]
    public void DiscoverFeatures_WithoutAttributeForMultipleFeatures_ReturnsAllWithDerivedNames()
    {
        // Arrange - create assembly with multiple features without attributes
        var assembly = TestAssemblyBuilder.CreateTestAssemblyWithoutAttribute("PaymentFeature", "ShippingFeature", "Inventory");

        // Act
        var features = CShells.Features.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        Assert.Equal(3, features.Count);
        Assert.Contains(features, f => f.Id == "Payment");
        Assert.Contains(features, f => f.Id == "Shipping");
        Assert.Contains(features, f => f.Id == "Inventory");
    }

    [Fact(DisplayName = "DiscoverFeatures with derived name collision throws InvalidOperationException")]
    public void DiscoverFeatures_WithDerivedNameCollision_ThrowsInvalidOperationException()
    {
        // Arrange - create assembly where two classes derive to same name (Payment and PaymentFeature)
        var assembly = TestAssemblyBuilder.CreateTestAssemblyWithoutAttribute("Payment", "PaymentFeature");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => CShells.Features.FeatureDiscovery.DiscoverFeatures([assembly]).ToList());
        Assert.Contains("Duplicate feature name", ex.Message);
        Assert.Contains("Payment", ex.Message);
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
        var ex = Assert.Throws<InvalidOperationException>(() => CShells.Features.FeatureDiscovery.DiscoverFeatures([assembly]).ToList());
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
        var features = CShells.Features.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

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
        var ex = Assert.Throws<InvalidOperationException>(() => CShells.Features.FeatureDiscovery.DiscoverFeatures([assembly]).ToList());
        Assert.Contains("odd number of metadata elements", ex.Message);
        Assert.Contains("FeatureWithOddMetadata", ex.Message);
    }
}
