using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CShells.Tests.Features;

/// <summary>
/// Tests for <see cref="IShellFeatureFactory"/> and <see cref="DefaultShellFeatureFactory"/>.
/// </summary>
public class ShellFeatureFactoryTests
{
    [Fact(DisplayName = "CreateFeature creates feature without ShellSettings when constructor doesn't require it")]
    public void CreateFeature_WithoutShellSettingsParameter_CreatesFeature()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var factory = new DefaultShellFeatureFactory(serviceProvider);

        // Act
        var feature = factory.CreateFeature<IShellFeature>(typeof(SimpleFeature));

        // Assert
        Assert.NotNull(feature);
        Assert.IsType<SimpleFeature>(feature);
    }

    [Fact(DisplayName = "CreateFeature creates feature with ShellSettings when constructor requires it")]
    public void CreateFeature_WithShellSettingsParameter_CreatesFeatureWithSettings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var factory = new DefaultShellFeatureFactory(serviceProvider);
        var shellSettings = new ShellSettings(new("TestShell"), ["Feature1"]);

        // Act
        var feature = factory.CreateFeature<IShellFeature>(typeof(FeatureWithShellSettings), shellSettings);

        // Assert
        Assert.NotNull(feature);
        var typedFeature = Assert.IsType<FeatureWithShellSettings>(feature);
        Assert.Same(shellSettings, typedFeature.Settings);
    }

    [Fact(DisplayName = "CreateFeature creates feature with dependencies from service provider")]
    public void CreateFeature_WithServiceDependencies_InjectsDependencies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var factory = new DefaultShellFeatureFactory(serviceProvider);

        // Act
        var feature = factory.CreateFeature<IShellFeature>(typeof(FeatureWithDependencies));

        // Assert
        Assert.NotNull(feature);
        var typedFeature = Assert.IsType<FeatureWithDependencies>(feature);
        Assert.NotNull(typedFeature.Logger);
    }

    [Fact(DisplayName = "CreateFeature creates feature with both ShellSettings and dependencies")]
    public void CreateFeature_WithShellSettingsAndDependencies_InjectsBoth()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var factory = new DefaultShellFeatureFactory(serviceProvider);
        var shellSettings = new ShellSettings(new("TestShell"), ["Feature1"]);

        // Act
        var feature = factory.CreateFeature<IShellFeature>(typeof(FeatureWithBoth), shellSettings);

        // Assert
        Assert.NotNull(feature);
        var typedFeature = Assert.IsType<FeatureWithBoth>(feature);
        Assert.Same(shellSettings, typedFeature.Settings);
        Assert.NotNull(typedFeature.Logger);
    }

    [Fact(DisplayName = "CreateFeature throws when feature type doesn't implement requested interface")]
    public void CreateFeature_WithInvalidType_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var factory = new DefaultShellFeatureFactory(serviceProvider);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            factory.CreateFeature<IWebShellFeature>(typeof(SimpleFeature)));
        Assert.Contains("does not implement", ex.Message);
    }

    [Fact(DisplayName = "CreateFeature throws when serviceProvider is null")]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new DefaultShellFeatureFactory(null!));
        Assert.Equal("serviceProvider", ex.ParamName);
    }

    [Fact(DisplayName = "CreateFeature throws when featureType is null")]
    public void CreateFeature_WithNullFeatureType_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var factory = new DefaultShellFeatureFactory(serviceProvider);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            factory.CreateFeature<IShellFeature>(null!));
        Assert.Equal("featureType", ex.ParamName);
    }

    // Test feature implementations
    private class SimpleFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) { }
    }

    private class FeatureWithShellSettings(ShellSettings settings) : IShellFeature
    {
        public ShellSettings Settings { get; } = settings;

        public void ConfigureServices(IServiceCollection services) { }
    }

    private class FeatureWithDependencies(ILogger<FeatureWithDependencies> logger) : IShellFeature
    {
        public ILogger<FeatureWithDependencies> Logger { get; } = logger;

        public void ConfigureServices(IServiceCollection services) { }
    }

    private class FeatureWithBoth(ILogger<FeatureWithBoth> logger, ShellSettings settings) : IShellFeature
    {
        public ShellSettings Settings { get; } = settings;
        public ILogger<FeatureWithBoth> Logger { get; } = logger;

        public void ConfigureServices(IServiceCollection services) { }
    }
}
