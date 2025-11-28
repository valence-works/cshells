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
        Assert.Equal("name", ex.ParamName);
    }

    [Theory]
    [InlineData("TestShell", "TESTSHELL", true)]
    [InlineData("TestShell", "testshell", true)]
    [InlineData("Shell1", "Shell2", false)]
    [InlineData("Shell1", "Shell1", true)]
    public void Equals_WithVariousNames_ReturnsExpectedResult(string name1, string name2, bool expectedEqual)
    {
        // Arrange
        var shellId1 = new ShellId(name1);
        var shellId2 = new ShellId(name2);

        // Act & Assert
        Assert.Equal(expectedEqual, shellId1.Equals(shellId2));
        Assert.Equal(expectedEqual, shellId1 == shellId2);
        Assert.Equal(!expectedEqual, shellId1 != shellId2);
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
