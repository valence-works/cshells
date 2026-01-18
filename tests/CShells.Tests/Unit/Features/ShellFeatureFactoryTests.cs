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

    [Fact(DisplayName = "CreateFeature creates feature with ShellFeatureContext when constructor requires it")]
    public void CreateFeature_WithShellFeatureContext_CreatesFeatureWithContext()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var factory = new DefaultShellFeatureFactory(serviceProvider);
        var shellSettings = new ShellSettings(new("TestShell"), ["Feature1"]);
        var featureDescriptors = new List<ShellFeatureDescriptor>
        {
            new("Feature1") { StartupType = typeof(SimpleFeature) },
            new("Feature2") { StartupType = typeof(FeatureWithShellSettings) }
        }.AsReadOnly();
        var context = new ShellFeatureContext(shellSettings, featureDescriptors);

        // Act
        var feature = factory.CreateFeature<IShellFeature>(typeof(FeatureWithContext), shellSettings, context);

        // Assert
        Assert.NotNull(feature);
        var typedFeature = Assert.IsType<FeatureWithContext>(feature);
        Assert.Same(context, typedFeature.Context);
        Assert.Same(shellSettings, typedFeature.Context.Settings);
        Assert.Equal(2, typedFeature.Context.AllFeatures.Count);
    }

    [Fact(DisplayName = "CreateFeature prefers ShellFeatureContext over ShellSettings when both are available")]
    public void CreateFeature_WithBothContextAndSettings_PrefersContext()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var factory = new DefaultShellFeatureFactory(serviceProvider);
        var shellSettings = new ShellSettings(new("TestShell"), ["Feature1"]);
        var featureDescriptors = new List<ShellFeatureDescriptor>
        {
            new("Feature1") { StartupType = typeof(SimpleFeature) }
        }.AsReadOnly();
        var context = new ShellFeatureContext(shellSettings, featureDescriptors);

        // Act - Feature constructor accepts ShellFeatureContext, so it should be injected
        var feature = factory.CreateFeature<IShellFeature>(typeof(FeatureWithContext), shellSettings, context);

        // Assert
        Assert.NotNull(feature);
        var typedFeature = Assert.IsType<FeatureWithContext>(feature);
        Assert.Same(context, typedFeature.Context);
    }

    [Fact(DisplayName = "CreateFeature with ShellFeatureContext and dependencies injects both")]
    public void CreateFeature_WithContextAndDependencies_InjectsBoth()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var factory = new DefaultShellFeatureFactory(serviceProvider);
        var shellSettings = new ShellSettings(new("TestShell"), ["Feature1"]);
        var featureDescriptors = new List<ShellFeatureDescriptor>().AsReadOnly();
        var context = new ShellFeatureContext(shellSettings, featureDescriptors);

        // Act
        var feature = factory.CreateFeature<IShellFeature>(typeof(FeatureWithContextAndDependencies), shellSettings, context);

        // Assert
        Assert.NotNull(feature);
        var typedFeature = Assert.IsType<FeatureWithContextAndDependencies>(feature);
        Assert.Same(context, typedFeature.Context);
        Assert.NotNull(typedFeature.Logger);
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

    private class FeatureWithContext(ShellFeatureContext context) : IShellFeature
    {
        public ShellFeatureContext Context { get; } = context;

        public void ConfigureServices(IServiceCollection services) { }
    }

    private class FeatureWithContextAndDependencies(ILogger<FeatureWithContextAndDependencies> logger, ShellFeatureContext context) : IShellFeature
    {
        public ShellFeatureContext Context { get; } = context;
        public ILogger<FeatureWithContextAndDependencies> Logger { get; } = logger;

        public void ConfigureServices(IServiceCollection services) { }
    }
}
