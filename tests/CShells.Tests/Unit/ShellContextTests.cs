using CShells.Hosting;

namespace CShells.Tests.Unit;

public class ShellContextTests
{
    [Fact(DisplayName = "Constructor with null settings throws ArgumentNullException")]
    public void Constructor_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new ShellContext(null!, serviceProvider, Array.Empty<string>()));
        Assert.Equal("settings", ex.ParamName);
    }

    [Fact(DisplayName = "Constructor with null service provider throws ArgumentNullException")]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new ShellSettings(new("Test"));

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new ShellContext(settings, null!, Array.Empty<string>()));
        Assert.Equal("serviceProvider", ex.ParamName);
    }

    [Fact(DisplayName = "Constructor sets properties")]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // Arrange
        var settings = new ShellSettings(new("TestShell"));
        var serviceProvider = new TestServiceProvider();

        // Act
        var context = new ShellContext(settings, serviceProvider, Array.Empty<string>());

        // Assert
        Assert.Same(settings, context.Settings);
        Assert.Same(serviceProvider, context.ServiceProvider);
    }

    private class TestServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
