namespace CShells.Tests.Unit;

public class ShellFeatureAttributeTests
{
    [Fact(DisplayName = "Constructor with null name throws ArgumentNullException")]
    public void Constructor_WithNullName_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new ShellFeatureAttribute(null!));
        Assert.Equal("name", ex.ParamName);
    }
}
