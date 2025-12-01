using CShells.Configuration;
using CShells.Hosting;
using CShells.Tests.Integration.ShellHost;

namespace CShells.Tests.Integration.DefaultShellHost;

/// <summary>
/// Tests for <see cref="DefaultShellHost"/> constructor validation.
/// </summary>
public class ConstructorTests
{
    [Fact(DisplayName = "Constructor with null shell settings throws ArgumentNullException")]
    public void Constructor_WithNullShellSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var (services, provider) = TestFixtures.CreateRootServices();
        var accessor = TestFixtures.CreateRootServicesAccessor(services);
        var factory = new CShells.Features.DefaultShellFeatureFactory(provider);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new Hosting.DefaultShellHost(null!, provider, accessor, factory));
        Assert.Equal("shellSettingsCache", ex.ParamName);
    }

    [Fact(DisplayName = "Constructor with null assemblies throws ArgumentNullException")]
    public void Constructor_WithNullAssemblies_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new[] { new ShellSettings(new("Test")) };
        var cache = new ShellSettingsCache();
        cache.Load(settings);
        IEnumerable<System.Reflection.Assembly>? nullAssemblies = null;
        var (services, provider) = TestFixtures.CreateRootServices();
        var accessor = TestFixtures.CreateRootServicesAccessor(services);
        var factory = new CShells.Features.DefaultShellFeatureFactory(provider);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new Hosting.DefaultShellHost(cache, nullAssemblies!, provider, accessor, factory));
        Assert.Equal("assemblies", ex.ParamName);
    }

    [Fact(DisplayName = "Constructor with null root provider throws ArgumentNullException")]
    public void Constructor_WithNullRootProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new[] { new ShellSettings(new("Test")) };
        var cache = new ShellSettingsCache();
        cache.Load(settings);
        var (services, provider) = TestFixtures.CreateRootServices();
        var accessor = TestFixtures.CreateRootServicesAccessor(services);
        var factory = new CShells.Features.DefaultShellFeatureFactory(provider);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new Hosting.DefaultShellHost(cache, [], null!, accessor, factory));
        Assert.Equal("rootProvider", ex.ParamName);
    }

    [Fact(DisplayName = "Constructor with null root services accessor throws ArgumentNullException")]
    public void Constructor_WithNullRootServicesAccessor_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new[] { new ShellSettings(new("Test")) };
        var cache = new ShellSettingsCache();
        cache.Load(settings);
        var (_, provider) = TestFixtures.CreateRootServices();
        var factory = new CShells.Features.DefaultShellFeatureFactory(provider);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new Hosting.DefaultShellHost(cache, [], provider, null!, factory));
        Assert.Equal("rootServicesAccessor", ex.ParamName);
    }
}
