using CShells.Tests.Integration.ShellHost;

namespace CShells.Tests.Integration.DefaultShellHost;

/// <summary>
/// Tests for <see cref="CShells.DefaultShellHost"/> constructor validation.
/// </summary>
public class ConstructorTests
{
    [Fact(DisplayName = "Constructor with null shell settings throws ArgumentNullException")]
    public void Constructor_WithNullShellSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var (services, provider) = TestFixtures.CreateRootServices();
        var accessor = TestFixtures.CreateRootServicesAccessor(services);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new CShells.DefaultShellHost(null!, provider, accessor));
        Assert.Equal("shellSettings", ex.ParamName);
    }

    [Fact(DisplayName = "Constructor with null assemblies throws ArgumentNullException")]
    public void Constructor_WithNullAssemblies_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new[] { new ShellSettings(new("Test")) };
        IEnumerable<System.Reflection.Assembly>? nullAssemblies = null;
        var (services, provider) = TestFixtures.CreateRootServices();
        var accessor = TestFixtures.CreateRootServicesAccessor(services);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new CShells.DefaultShellHost(settings, nullAssemblies!, provider, accessor));
        Assert.Equal("assemblies", ex.ParamName);
    }

    [Fact(DisplayName = "Constructor with null root provider throws ArgumentNullException")]
    public void Constructor_WithNullRootProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new[] { new ShellSettings(new("Test")) };
        var (services, _) = TestFixtures.CreateRootServices();
        var accessor = TestFixtures.CreateRootServicesAccessor(services);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new CShells.DefaultShellHost(settings, [], null!, accessor));
        Assert.Equal("rootProvider", ex.ParamName);
    }

    [Fact(DisplayName = "Constructor with null root services accessor throws ArgumentNullException")]
    public void Constructor_WithNullRootServicesAccessor_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new[] { new ShellSettings(new("Test")) };
        var (services, provider) = TestFixtures.CreateRootServices();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new CShells.DefaultShellHost(settings, [], provider, null!));
        Assert.Equal("rootServicesAccessor", ex.ParamName);
    }
}
