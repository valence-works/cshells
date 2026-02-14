using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using CShells.Configuration;

namespace CShells.Tests.Unit;

public class ShellBuilderTests
{
    [Fact(DisplayName = "ShellBuilder initializes with ShellId")]
    public void ShellBuilder_InitializesWithShellId()
    {
        // Arrange & Act
        var builder = new ShellBuilder("TestShell");
        var settings = builder.Build();

        // Assert
        Assert.Equal("TestShell", settings.Id.Name);
        Assert.Empty(settings.EnabledFeatures);
        Assert.Empty(settings.ConfigurationData);
    }

    [Fact(DisplayName = "WithFeatures sets enabled features")]
    public void WithFeatures_SetsEnabledFeatures()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell");

        // Act
        builder.WithFeatures("Feature1", "Feature2");
        var settings = builder.Build();

        // Assert
        Assert.Equal(["Feature1", "Feature2"], settings.EnabledFeatures);
    }

    [Fact(DisplayName = "WithFeature adds single feature")]
    public void WithFeature_AddsSingleFeature()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell")
            .WithFeatures("Feature1");

        // Act
        builder.WithFeature("Feature2");
        var settings = builder.Build();

        // Assert
        Assert.Equal(["Feature1", "Feature2"], settings.EnabledFeatures);
    }

    [Fact(DisplayName = "WithConfiguration adds configuration entry")]
    public void WithConfiguration_AddsConfigurationEntry()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell");

        // Act
        builder.WithConfiguration("Key1", "Value1");
        var settings = builder.Build();

        // Assert
        Assert.Single(settings.ConfigurationData);
        Assert.Equal("Value1", settings.ConfigurationData["Key1"]);
    }

    [Fact(DisplayName = "WithConfiguration with dictionary adds multiple entries")]
    public void WithConfiguration_Dictionary_AddsMultipleEntries()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell");
        var configuration = new Dictionary<string, object>
        {
            ["Key1"] = "Value1",
            ["Key2"] = "Value2"
        };

        // Act
        builder.WithConfiguration(configuration);
        var settings = builder.Build();

        // Assert
        Assert.Equal(2, settings.ConfigurationData.Count);
        Assert.Equal("Value1", settings.ConfigurationData["Key1"]);
        Assert.Equal("Value2", settings.ConfigurationData["Key2"]);
    }

    [Fact(DisplayName = "WithConfiguration adds colon-separated keys")]
    public void WithConfiguration_ColonSeparatedKeys_Works()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell");

        // Act
        builder.WithConfiguration("WebRouting:Path", "acme");
        var settings = builder.Build();

        // Assert
        Assert.Single(settings.ConfigurationData);
        Assert.Equal("acme", settings.ConfigurationData["WebRouting:Path"]);
    }

    [Fact(DisplayName = "FromConfiguration merges features without duplicates")]
    public void FromConfiguration_MergesFeatures_WithoutDuplicates()
    {
        // Arrange
        var json = @"{
            ""Shell"": {
                ""Name"": ""TestShell"",
                ""Features"": [ ""Feature2"", ""Feature3"" ]
            }
        }";
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var config = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        var builder = new ShellBuilder("TestShell")
            .WithFeatures("Feature1", "Feature2");

        // Act
        builder.FromConfiguration(config.GetSection("Shell"));
        var settings = builder.Build();

        // Assert - Features should be merged and deduplicated
        Assert.Equal(["Feature1", "Feature2", "Feature3"], settings.EnabledFeatures);
    }

    [Fact(DisplayName = "FromConfiguration with IConfigurationSection merges configuration with precedence")]
    public void FromConfiguration_MergesConfiguration_WithPrecedence()
    {
        // Arrange
        var json = @"{
            ""Shell"": {
                ""Name"": ""TestShell"",
                ""Configuration"": {
                    ""Key1"": ""NewValue1"",
                    ""Key2"": ""Value2""
                }
            }
        }";
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var config = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        var builder = new ShellBuilder("TestShell")
            .WithConfiguration("Key1", "OriginalValue1");

        // Act
        builder.FromConfiguration(config.GetSection("Shell"));
        var settings = builder.Build();

        // Assert - Configuration from section should take precedence
        Assert.Equal(2, settings.ConfigurationData.Count);
        Assert.Equal("NewValue1", settings.ConfigurationData["Key1"]);
        Assert.Equal("Value2", settings.ConfigurationData["Key2"]);
    }

    [Fact(DisplayName = "FromConfiguration with IConfigurationSection loads feature settings into ConfigurationData")]
    public void FromConfiguration_LoadsFeatureSettings_IntoConfigurationData()
    {
        // Arrange
        var json = @"{
            ""Shell"": {
                ""Name"": ""TestShell"",
                ""Features"": [
                    { ""Name"": ""Database"", ""ConnectionString"": ""Server=localhost"", ""Timeout"": ""30"" },
                    ""SimpleFeature""
                ]
            }
        }";
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var config = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        var builder = new ShellBuilder("TestShell");

        // Act
        builder.FromConfiguration(config.GetSection("Shell"));
        var settings = builder.Build();

        // Assert - Feature settings should be flattened under feature name
        Assert.Equal(2, settings.ConfigurationData.Count);
        Assert.Equal("Server=localhost", settings.ConfigurationData["Database:ConnectionString"]);
        Assert.Equal("30", settings.ConfigurationData["Database:Timeout"]);
        Assert.Equal(["Database", "SimpleFeature"], settings.EnabledFeatures);
    }

    [Fact(DisplayName = "FromConfiguration with ShellConfig merges all data")]
    public void FromConfiguration_WithShellConfig_MergesAllData()
    {
        // Arrange
        var config = new ShellConfig
        {
            Name = "TestShell",
            Features =
            [
                FeatureEntry.FromName("Feature2"),
                new FeatureEntry
                {
                    Name = "Feature3",
                    Settings = new()
                    {
                        ["Setting1"] = "SettingValue1",
                        ["Setting2"] = "SettingValue2"
                    }
                }
            ],
            Configuration = new Dictionary<string, object?>
            {
                ["Key1"] = "NewValue1",
                ["Key2"] = "Value2"
            }
        };

        var builder = new ShellBuilder("TestShell")
            .WithFeatures("Feature1", "Feature2")
            .WithConfiguration("Key1", "OriginalValue1");

        // Act
        builder.FromConfiguration(config);
        var settings = builder.Build();

        // Assert - Features merged, configuration and feature settings loaded
        Assert.Equal(["Feature1", "Feature2", "Feature3"], settings.EnabledFeatures);
        Assert.Equal(4, settings.ConfigurationData.Count); // Key1, Key2, Feature3:Setting1, Feature3:Setting2
        Assert.Equal("NewValue1", settings.ConfigurationData["Key1"]);
        Assert.Equal("Value2", settings.ConfigurationData["Key2"]);
        Assert.Equal("SettingValue1", settings.ConfigurationData["Feature3:Setting1"]);
        Assert.Equal("SettingValue2", settings.ConfigurationData["Feature3:Setting2"]);
    }

    [Fact(DisplayName = "FromConfiguration with ShellConfig ignores null settings")]
    public void FromConfiguration_WithShellConfig_IgnoresNullSettings()
    {
        // Arrange
        var config = new ShellConfig
        {
            Name = "TestShell",
            Features =
            [
                new FeatureEntry
                {
                    Name = "Feature1",
                    Settings = new()
                    {
                        ["Setting1"] = "Value1",
                        ["Setting2"] = null,
                        ["Setting3"] = "Value3"
                    }
                }
            ]
        };

        var builder = new ShellBuilder("TestShell");

        // Act
        builder.FromConfiguration(config);
        var settings = builder.Build();

        // Assert - Null settings should be filtered out
        Assert.Equal(2, settings.ConfigurationData.Count);
        Assert.Equal("Value1", settings.ConfigurationData["Feature1:Setting1"]);
        Assert.Equal("Value3", settings.ConfigurationData["Feature1:Setting3"]);
        Assert.False(settings.ConfigurationData.ContainsKey("Feature1:Setting2"));
    }

    [Fact(DisplayName = "Multiple FromConfiguration calls accumulate features")]
    public void MultipleFromConfiguration_AccumulatesFeatures()
    {
        // Arrange
        var config1 = new ShellConfig
        {
            Name = "TestShell",
            Features = [FeatureEntry.FromName("Feature1"), FeatureEntry.FromName("Feature2")]
        };

        var config2 = new ShellConfig
        {
            Name = "TestShell",
            Features = [FeatureEntry.FromName("Feature2"), FeatureEntry.FromName("Feature3")]
        };

        var builder = new ShellBuilder("TestShell");

        // Act
        builder.FromConfiguration(config1)
               .FromConfiguration(config2);
        var settings = builder.Build();

        // Assert - Features should be merged and deduplicated
        Assert.Equal(["Feature1", "Feature2", "Feature3"], settings.EnabledFeatures);
    }

    [Fact(DisplayName = "FromConfiguration handles nested configuration")]
    public void FromConfiguration_HandlesNestedConfiguration()
    {
        // Arrange
        var json = @"{
            ""Shell"": {
                ""Name"": ""TestShell"",
                ""Configuration"": {
                    ""WebRouting"": {
                        ""Path"": ""acme"",
                        ""HeaderName"": ""X-Tenant-Id""
                    }
                }
            }
        }";
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var config = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        var builder = new ShellBuilder("TestShell");

        // Act
        builder.FromConfiguration(config.GetSection("Shell"));
        var settings = builder.Build();

        // Assert - Nested configuration should be flattened
        Assert.Equal(2, settings.ConfigurationData.Count);
        Assert.Equal("acme", settings.ConfigurationData["WebRouting:Path"]);
        Assert.Equal("X-Tenant-Id", settings.ConfigurationData["WebRouting:HeaderName"]);
    }

    [Fact(DisplayName = "Implicit conversion to ShellSettings works")]
    public void ImplicitConversion_ToShellSettings_Works()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell")
            .WithFeatures("Feature1");

        // Act
        ShellSettings settings = builder;

        // Assert
        Assert.Equal("TestShell", settings.Id.Name);
        Assert.Equal(["Feature1"], settings.EnabledFeatures);
    }

    [Fact(DisplayName = "WithFeature with configure action adds feature and settings")]
    public void WithFeature_WithConfigureAction_AddsFeatureAndSettings()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell");

        // Act
        builder.WithFeature("FraudDetection", settings =>
        {
            settings.WithSetting("Threshold", 0.85);
            settings.WithSetting("MaxAmount", 5000);
        });
        var shellSettings = builder.Build();

        // Assert
        Assert.Equal(["FraudDetection"], shellSettings.EnabledFeatures);
        Assert.Equal(2, shellSettings.ConfigurationData.Count);
        Assert.Equal(0.85, shellSettings.ConfigurationData["FraudDetection:Threshold"]);
        Assert.Equal(5000, shellSettings.ConfigurationData["FraudDetection:MaxAmount"]);
    }

    [Fact(DisplayName = "WithFeature with FeatureEntry adds feature and settings")]
    public void WithFeature_WithFeatureEntry_AddsFeatureAndSettings()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell");
        var featureEntry = new FeatureEntry
        {
            Name = "Database",
            Settings = new() { ["ConnectionString"] = "Server=localhost" }
        };

        // Act
        builder.WithFeature(featureEntry);
        var shellSettings = builder.Build();

        // Assert
        Assert.Equal(["Database"], shellSettings.EnabledFeatures);
        Assert.Single(shellSettings.ConfigurationData);
        Assert.Equal("Server=localhost", shellSettings.ConfigurationData["Database:ConnectionString"]);
    }

    [Fact(DisplayName = "WithFeature does not duplicate feature names")]
    public void WithFeature_DoesNotDuplicateFeatureNames()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell")
            .WithFeature("Feature1");

        // Act
        builder.WithFeature("Feature1", settings => settings.WithSetting("Key", "Value"));
        var shellSettings = builder.Build();

        // Assert - Feature1 should appear only once
        Assert.Equal(["Feature1"], shellSettings.EnabledFeatures);
    }

    [Fact(DisplayName = "Guard clauses throw ArgumentNullException")]
    public void GuardClauses_ThrowArgumentNullException()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithFeatures(null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithFeature((string)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithFeature("Feature", (Action<FeatureSettingsBuilder>)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithFeature((FeatureEntry)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithConfiguration(null!, "value"));
        Assert.Throws<ArgumentNullException>(() => builder.WithConfiguration("key", null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithConfiguration((IDictionary<string, object>)null!));
        Assert.Throws<ArgumentNullException>(() => builder.FromConfiguration((IConfigurationSection)null!));
        Assert.Throws<ArgumentNullException>(() => builder.FromConfiguration((ShellConfig)null!));
    }
}
