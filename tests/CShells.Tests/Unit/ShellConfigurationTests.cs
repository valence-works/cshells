using CShells.Configuration;
using Microsoft.Extensions.Configuration;

namespace CShells.Tests.Unit;

public class ShellConfigurationTests
{
    [Fact(DisplayName = "ShellSettings.GetConfigurationRoot provides IConfiguration view")]
    public void ShellSettings_GetConfigurationRoot_ProvidesIConfigurationView()
    {
        // Arrange
        var shellSettings = new ShellSettings
        {
            Id = new ShellId("TestShell"),
            ConfigurationData = new Dictionary<string, object>
            {
                ["MyFeature:ApiKey"] = "secret-key",
                ["MyFeature:MaxRetries"] = "3"
            }
        };

        // Act - Access configuration directly from ShellSettings
        var config = shellSettings.GetConfigurationRoot();
        var section = config.GetSection("MyFeature");

        // Assert
        Assert.True(section.Exists());
        Assert.Equal("secret-key", section["ApiKey"]);
        Assert.Equal("3", section["MaxRetries"]);
    }

    [Fact(DisplayName = "ShellSettings.GetConfigurationRoot allows Bind() for options")]
    public void ShellSettings_GetConfigurationRoot_AllowsBindForOptions()
    {
        // Arrange - This simulates what a feature developer would do
        var shellSettings = new ShellSettings
        {
            Id = new ShellId("TestShell"),
            ConfigurationData = new Dictionary<string, object>
            {
                ["FraudDetection:Threshold"] = "0.85",
                ["FraudDetection:MaxTransactionAmount"] = "5000",
                ["FraudDetection:EnableLogging"] = "true"
            }
        };

        // Act - Bind options directly from ShellSettings.GetConfigurationRoot()
        var options = new FraudDetectionOptions();
        shellSettings.GetConfigurationRoot().GetSection("FraudDetection").Bind(options);

        // Assert
        Assert.Equal(0.85, options.Threshold);
        Assert.Equal(5000, options.MaxTransactionAmount);
        Assert.True(options.EnableLogging);
    }

    [Fact(DisplayName = "ShellSettings.GetConfigurationRoot is cached")]
    public void ShellSettings_GetConfigurationRoot_IsCached()
    {
        // Arrange
        var shellSettings = new ShellSettings
        {
            Id = new ShellId("TestShell"),
            ConfigurationData = new Dictionary<string, object>
            {
                ["Key"] = "Value1"
            }
        };

        // Act - Get configuration (should be cached)
        var config1 = shellSettings.GetConfigurationRoot();
        var config2 = shellSettings.GetConfigurationRoot();

        // Assert - Same instance (cached)
        Assert.Same(config1, config2);
    }

    [Fact(DisplayName = "ShellConfiguration provides feature settings via GetSection")]
    public void ShellConfiguration_ProvidesFeatureSettings_ViaGetSection()
    {
        // Arrange - Create shell settings with feature-specific configuration
        var shellSettings = new ShellSettings
        {
            Id = new ShellId("TestShell"),
            ConfigurationData = new Dictionary<string, object>
            {
                ["FraudDetection:Threshold"] = "0.85",
                ["FraudDetection:MaxTransactionAmount"] = "5000",
                ["Database:ConnectionString"] = "Server=localhost"
            }
        };

        var rootConfig = new ConfigurationBuilder().Build();
        var shellConfig = new ShellConfiguration(shellSettings, rootConfig);

        // Act - Get section for FraudDetection feature
        var fraudSection = shellConfig.GetSection("FraudDetection");

        // Assert - Section should exist and contain the settings
        Assert.True(fraudSection.Exists());
        Assert.Equal("0.85", fraudSection["Threshold"]);
        Assert.Equal("5000", fraudSection["MaxTransactionAmount"]);
    }

    [Fact(DisplayName = "ShellConfiguration GetSection works with nested configuration")]
    public void ShellConfiguration_GetSection_WorksWithNestedConfiguration()
    {
        // Arrange
        var shellSettings = new ShellSettings
        {
            Id = new ShellId("TestShell"),
            ConfigurationData = new Dictionary<string, object>
            {
                ["WebRouting:Path"] = "acme",
                ["WebRouting:HeaderName"] = "X-Tenant-Id"
            }
        };

        var rootConfig = new ConfigurationBuilder().Build();
        var shellConfig = new ShellConfiguration(shellSettings, rootConfig);

        // Act
        var webRoutingSection = shellConfig.GetSection("WebRouting");

        // Assert
        Assert.True(webRoutingSection.Exists());
        Assert.Equal("acme", webRoutingSection["Path"]);
        Assert.Equal("X-Tenant-Id", webRoutingSection["HeaderName"]);
    }

    [Fact(DisplayName = "ShellConfiguration can bind options from feature section")]
    public void ShellConfiguration_CanBindOptions_FromFeatureSection()
    {
        // Arrange
        var shellSettings = new ShellSettings
        {
            Id = new ShellId("TestShell"),
            ConfigurationData = new Dictionary<string, object>
            {
                ["FraudDetection:Threshold"] = "0.85",
                ["FraudDetection:MaxTransactionAmount"] = "5000",
                ["FraudDetection:EnableLogging"] = "true"
            }
        };

        var rootConfig = new ConfigurationBuilder().Build();
        var shellConfig = new ShellConfiguration(shellSettings, rootConfig);

        // Act - Bind to options object
        var options = new FraudDetectionOptions();
        shellConfig.GetSection("FraudDetection").Bind(options);

        // Assert
        Assert.Equal(0.85, options.Threshold);
        Assert.Equal(5000, options.MaxTransactionAmount);
        Assert.True(options.EnableLogging);
    }

    [Fact(DisplayName = "ShellConfiguration merges with root configuration")]
    public void ShellConfiguration_MergesWithRootConfiguration()
    {
        // Arrange
        var shellSettings = new ShellSettings
        {
            Id = new ShellId("TestShell"),
            ConfigurationData = new Dictionary<string, object>
            {
                ["Feature:SettingA"] = "ShellValue"
            }
        };

        var rootConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Feature:SettingB"] = "RootValue",
                ["GlobalSetting"] = "GlobalValue"
            })
            .Build();

        var shellConfig = new ShellConfiguration(shellSettings, rootConfig);

        // Act & Assert - Shell settings take precedence
        Assert.Equal("ShellValue", shellConfig["Feature:SettingA"]);
        
        // Root settings are still accessible
        Assert.Equal("GlobalValue", shellConfig["GlobalSetting"]);
    }

    [Fact(DisplayName = "Feature properties can be bound from ConfigurationData")]
    public void FeatureProperties_CanBeBound_FromConfigurationData()
    {
        // Arrange - Simulate what happens when a feature has public properties
        var shellSettings = new ShellSettings
        {
            Id = new ShellId("TestShell"),
            ConfigurationData = new Dictionary<string, object>
            {
                ["MyFeature:ApiKey"] = "secret-key",
                ["MyFeature:MaxRetries"] = "3",
                ["MyFeature:Timeout"] = "00:00:30"
            }
        };

        var rootConfig = new ConfigurationBuilder().Build();
        var shellConfig = new ShellConfiguration(shellSettings, rootConfig);

        // Act - Get the feature section and bind properties
        var featureSection = shellConfig.GetSection("MyFeature");

        // Assert
        Assert.True(featureSection.Exists());
        Assert.Equal("secret-key", featureSection["ApiKey"]);
        Assert.Equal("3", featureSection["MaxRetries"]);
        Assert.Equal("00:00:30", featureSection["Timeout"]);

        // Can also bind to an object
        var featureConfig = new MyFeatureConfig();
        featureSection.Bind(featureConfig);
        Assert.Equal("secret-key", featureConfig.ApiKey);
        Assert.Equal(3, featureConfig.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(30), featureConfig.Timeout);
    }

    private class FraudDetectionOptions
    {
        public double Threshold { get; set; }
        public int MaxTransactionAmount { get; set; }
        public bool EnableLogging { get; set; }
    }

    private class MyFeatureConfig
    {
        public string ApiKey { get; set; } = "";
        public int MaxRetries { get; set; }
        public TimeSpan Timeout { get; set; }
    }
}

