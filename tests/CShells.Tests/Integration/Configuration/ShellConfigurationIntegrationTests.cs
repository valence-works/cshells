using CShells.Configuration;
using CShells.Features;
using CShells.Hosting;
using CShells.Tests.Integration.ShellHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.Configuration;

public class ShellConfigurationIntegrationTests
{
    [Fact(DisplayName = "Shell-scoped IConfiguration is available in shell service provider")]
    public void ShellScopedConfiguration_IsAvailableInShellServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Root configuration
        var rootConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Theme"] = "Light",
                ["MaxUploadSizeMB"] = "10"
            })
            .Build();

        services.AddSingleton<IConfiguration>(rootConfig);

        var rootProvider = services.BuildServiceProvider();

        // Shell with custom settings
        var shellSettings = new ShellSettings(new ShellId("TestShell"));
        shellSettings.ConfigurationData["Theme"] = "Dark";
        shellSettings.ConfigurationData["MaxUploadSizeMB"] = "50";

        var cache = new ShellSettingsCache();
        cache.Load([shellSettings]);

        var accessor = TestFixtures.CreateRootServicesAccessor(services);
        var factory = new DefaultShellFeatureFactory(rootProvider);
        var exclusionRegistry = new ShellServiceExclusionRegistry([]);
        var shellHost = new Hosting.DefaultShellHost(cache, [], rootProvider, accessor, factory, exclusionRegistry);

        // Act
        var shellContext = shellHost.GetShell(shellSettings.Id);
        var shellConfig = shellContext.ServiceProvider.GetRequiredService<IConfiguration>();

        // Assert
        Assert.NotNull(shellConfig);
        Assert.Equal("Dark", shellConfig["Theme"]);
        Assert.Equal("50", shellConfig["MaxUploadSizeMB"]);

        // Clean up
        shellHost.Dispose();
        rootProvider.Dispose();
    }

    [Fact(DisplayName = "Different shells can have different configuration values")]
    public void DifferentShells_CanHaveDifferentConfigurationValues()
    {
        // Arrange
        var services = new ServiceCollection();

        var rootConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Theme"] = "Light"
            })
            .Build();

        services.AddSingleton<IConfiguration>(rootConfig);

        var rootProvider = services.BuildServiceProvider();

        // Create two shells with different settings
        var shell1 = new ShellSettings(new ShellId("Shell1"));
        shell1.ConfigurationData["Theme"] = "Dark";

        var shell2 = new ShellSettings(new ShellId("Shell2"));
        shell2.ConfigurationData["Theme"] = "Blue";

        var cache = new ShellSettingsCache();
        cache.Load([shell1, shell2]);

        var accessor = TestFixtures.CreateRootServicesAccessor(services);
        var factory = new DefaultShellFeatureFactory(rootProvider);
        var exclusionRegistry = new ShellServiceExclusionRegistry([]);
        var shellHost = new Hosting.DefaultShellHost(cache, [], rootProvider, accessor, factory, exclusionRegistry);

        // Act
        var context1 = shellHost.GetShell(shell1.Id);
        var context2 = shellHost.GetShell(shell2.Id);

        var config1 = context1.ServiceProvider.GetRequiredService<IConfiguration>();
        var config2 = context2.ServiceProvider.GetRequiredService<IConfiguration>();

        // Assert
        Assert.Equal("Dark", config1["Theme"]);
        Assert.Equal("Blue", config2["Theme"]);

        // Clean up
        shellHost.Dispose();
        rootProvider.Dispose();
    }

    [Fact(DisplayName = "Shell without custom settings inherits root configuration")]
    public void ShellWithoutCustomSettings_InheritsRootConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();

        var rootConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Theme"] = "Light",
                ["MaxUploadSizeMB"] = "10"
            })
            .Build();

        services.AddSingleton<IConfiguration>(rootConfig);

        var rootProvider = services.BuildServiceProvider();

        // Create shell without custom settings
        var shellSettings = new ShellSettings(new ShellId("TestShell"));

        var cache = new ShellSettingsCache();
        cache.Load([shellSettings]);

        var accessor = TestFixtures.CreateRootServicesAccessor(services);
        var factory = new DefaultShellFeatureFactory(rootProvider);
        var exclusionRegistry = new ShellServiceExclusionRegistry([]);
        var shellHost = new Hosting.DefaultShellHost(cache, [], rootProvider, accessor, factory, exclusionRegistry);

        // Act
        var shellContext = shellHost.GetShell(shellSettings.Id);
        var shellConfig = shellContext.ServiceProvider.GetRequiredService<IConfiguration>();

        // Assert - should get root values
        Assert.Equal("Light", shellConfig["Theme"]);
        Assert.Equal("10", shellConfig["MaxUploadSizeMB"]);

        // Clean up
        shellHost.Dispose();
        rootProvider.Dispose();
    }
}
