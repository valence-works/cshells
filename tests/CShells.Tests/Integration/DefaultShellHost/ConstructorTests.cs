namespace CShells.Tests.Integration.DefaultShellHost;

/// <summary>
/// Tests for <see cref="CShells.DefaultShellHost"/> constructor validation.
/// </summary>
public class ConstructorTests
{
    [Fact(DisplayName = "Constructor with null shell settings throws ArgumentNullException")]
    public void Constructor_WithNullShellSettings_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new CShells.DefaultShellHost(null!));
        Assert.Equal("shellSettings", ex.ParamName);
    }

    [Fact(DisplayName = "Constructor with null assemblies throws ArgumentNullException")]
    public void Constructor_WithNullAssemblies_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new[] { new ShellSettings(new("Test")) };
        IEnumerable<System.Reflection.Assembly>? nullAssemblies = null;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new CShells.DefaultShellHost(settings, nullAssemblies!));
        Assert.Equal("assemblies", ex.ParamName);
    }
}
