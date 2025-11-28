namespace CShells.Tests;

public class ShellIdTests
{
    private const string TestName = "TestShell";

    [Fact]
    public void Constructor_WithValidName_SetsName()
    {
        // Act
        var shellId = new ShellId(TestName);

        // Assert
        Assert.Equal(TestName, shellId.Name);
    }

    [Fact]
    public void Constructor_WithNullName_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new ShellId(null!));
        Assert.Equal("Name", ex.ParamName);
    }

    [Fact]
    public void Equals_WithSameNameDifferentCase_ReturnsTrue()
    {
        // Arrange
        var shellId1 = new ShellId(TestName);
        var shellId2 = new ShellId(TestName.ToUpperInvariant());

        // Act & Assert
        Assert.Equal(shellId1, shellId2);
        Assert.True(shellId1 == shellId2);
        Assert.False(shellId1 != shellId2);
    }

    [Fact]
    public void Equals_WithDifferentNames_ReturnsFalse()
    {
        // Arrange
        var shellId1 = new ShellId("Shell1");
        var shellId2 = new ShellId("Shell2");

        // Act & Assert
        Assert.NotEqual(shellId1, shellId2);
        Assert.False(shellId1 == shellId2);
        Assert.True(shellId1 != shellId2);
    }

    [Fact]
    public void GetHashCode_WithSameNameDifferentCase_ReturnsSameHashCode()
    {
        // Arrange
        var shellId1 = new ShellId(TestName);
        var shellId2 = new ShellId(TestName.ToUpperInvariant());

        // Act & Assert
        Assert.Equal(shellId1.GetHashCode(), shellId2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsName()
    {
        // Arrange
        var shellId = new ShellId(TestName);

        // Act & Assert
        Assert.Equal(TestName, shellId.ToString());
    }

    [Fact]
    public void Equals_WithObject_ReturnsCorrectResult()
    {
        // Arrange
        var shellId1 = new ShellId(TestName);
        object shellId2 = new ShellId(TestName);
        object notShellId = TestName;

        // Act & Assert
        Assert.Equal(shellId1, shellId2);
        Assert.NotEqual(shellId1, notShellId);
    }
}
