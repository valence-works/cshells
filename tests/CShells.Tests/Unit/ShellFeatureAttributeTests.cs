namespace CShells.Tests.Unit;

public class ShellFeatureAttributeTests
{
    private const string TestFeatureName = "TestFeature";

    [Fact(DisplayName = "Constructor with valid name sets Name property")]
    public void Constructor_WithValidName_SetsName()
    {
        // Act
        var attribute = new ShellFeatureAttribute(TestFeatureName);

        // Assert
        Assert.Equal(TestFeatureName, attribute.Name);
    }

    [Fact(DisplayName = "Constructor with null name throws ArgumentNullException")]
    public void Constructor_WithNullName_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new ShellFeatureAttribute(null!));
        Assert.Equal("name", ex.ParamName);
    }

    [Theory(DisplayName = "Array properties default to empty arrays")]
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
}
