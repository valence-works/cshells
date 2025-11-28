namespace CShells.Tests;

public class ShellFeatureAttributeTests
{
    private const string TestFeatureName = "TestFeature";

    [Fact]
    public void Constructor_WithValidName_SetsName()
    {
        // Act
        var attribute = new ShellFeatureAttribute(TestFeatureName);

        // Assert
        Assert.Equal(TestFeatureName, attribute.Name);
    }

    [Theory]
    [InlineData(nameof(ShellFeatureAttribute.DependsOn))]
    [InlineData(nameof(ShellFeatureAttribute.Metadata))]
    public void ArrayProperty_DefaultsToEmptyArray(string propertyName)
    {
        // Arrange
        var attribute = new ShellFeatureAttribute(TestFeatureName);

        // Act
        var property = typeof(ShellFeatureAttribute).GetProperty(propertyName)!;
        var value = property.GetValue(attribute) as Array;

        // Assert
        Assert.NotNull(value);
        Assert.Empty(value);
    }

    [Fact]
    public void DependsOn_CanBeSet()
    {
        // Arrange
        var attribute = new ShellFeatureAttribute(TestFeatureName);
        var dependencies = new[] { "Feature1", "Feature2" };

        // Act
        attribute.DependsOn = dependencies;

        // Assert
        Assert.Equal(dependencies, attribute.DependsOn);
    }

    [Fact]
    public void Metadata_CanBeSet()
    {
        // Arrange
        var attribute = new ShellFeatureAttribute(TestFeatureName);
        var metadata = new object[] { "key1", "value1", "key2", 42 };

        // Act
        attribute.Metadata = metadata;

        // Assert
        Assert.Equal(metadata, attribute.Metadata);
    }

    [Fact]
    public void Attribute_CanBeAppliedToClass()
    {
        // Arrange & Act
        var attribute = typeof(TestFeatureClass).GetCustomAttributes(typeof(ShellFeatureAttribute), false)
            .Cast<ShellFeatureAttribute>()
            .FirstOrDefault();

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal("TestFeature", attribute.Name);
        Assert.Equal(["DependencyFeature"], attribute.DependsOn);
    }

    [ShellFeature("TestFeature", DependsOn = ["DependencyFeature"])]
    private class TestFeatureClass
    {
    }
}
