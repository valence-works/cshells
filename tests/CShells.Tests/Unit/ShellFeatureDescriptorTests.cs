namespace CShells.Tests.Unit;

public class ShellFeatureDescriptorTests
{
    [Fact(DisplayName = "Constructor with null ID throws ArgumentNullException")]
    public void Constructor_WithNullId_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new ShellFeatureDescriptor(null!));
        Assert.Equal("id", ex.ParamName);
    }
}
