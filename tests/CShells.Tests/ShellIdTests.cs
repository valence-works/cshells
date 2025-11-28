using FluentAssertions;

namespace CShells.Tests;

public class ShellIdTests
{
    [Fact]
    public void Constructor_WithValidName_SetsName()
    {
        // Arrange
        const string name = "TestShell";

        // Act
        var shellId = new ShellId(name);

        // Assert
        shellId.Name.Should().Be(name);
    }

    [Fact]
    public void Constructor_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new ShellId(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Equals_WithSameNameDifferentCase_ReturnsTrue()
    {
        // Arrange
        var shellId1 = new ShellId("TestShell");
        var shellId2 = new ShellId("TESTSHELL");

        // Act & Assert
        shellId1.Equals(shellId2).Should().BeTrue();
        (shellId1 == shellId2).Should().BeTrue();
        (shellId1 != shellId2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentNames_ReturnsFalse()
    {
        // Arrange
        var shellId1 = new ShellId("Shell1");
        var shellId2 = new ShellId("Shell2");

        // Act & Assert
        shellId1.Equals(shellId2).Should().BeFalse();
        (shellId1 == shellId2).Should().BeFalse();
        (shellId1 != shellId2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WithSameNameDifferentCase_ReturnsSameHashCode()
    {
        // Arrange
        var shellId1 = new ShellId("TestShell");
        var shellId2 = new ShellId("TESTSHELL");

        // Act & Assert
        shellId1.GetHashCode().Should().Be(shellId2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsName()
    {
        // Arrange
        const string name = "TestShell";
        var shellId = new ShellId(name);

        // Act & Assert
        shellId.ToString().Should().Be(name);
    }

    [Fact]
    public void Equals_WithObject_ReturnsCorrectResult()
    {
        // Arrange
        var shellId1 = new ShellId("TestShell");
        object shellId2 = new ShellId("TestShell");
        object notShellId = "TestShell";

        // Act & Assert
        shellId1.Equals(shellId2).Should().BeTrue();
        shellId1.Equals(notShellId).Should().BeFalse();
        shellId1.Equals(null).Should().BeFalse();
    }
}
