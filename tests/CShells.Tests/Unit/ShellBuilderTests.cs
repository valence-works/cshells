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
        Assert.Empty(settings.Properties);
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

    [Fact(DisplayName = "WithProperty adds property")]
    public void WithProperty_AddsProperty()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell");

        // Act
        builder.WithProperty("Key1", "Value1");
        var settings = builder.Build();

        // Assert
        Assert.Single(settings.Properties);
        Assert.Equal("Value1", settings.Properties["Key1"]);
    }

    [Fact(DisplayName = "WithProperties adds multiple properties")]
    public void WithProperties_AddsMultipleProperties()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell");
        var properties = new Dictionary<string, object>
        {
            ["Key1"] = "Value1",
            ["Key2"] = "Value2"
        };

        // Act
        builder.WithProperties(properties);
        var settings = builder.Build();

        // Assert
        Assert.Equal(2, settings.Properties.Count);
        Assert.Equal("Value1", settings.Properties["Key1"]);
        Assert.Equal("Value2", settings.Properties["Key2"]);
    }

    [Fact(DisplayName = "WithConfigurationData adds configuration data")]
    public void WithConfigurationData_AddsConfigurationData()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell");

        // Act
        builder.WithConfigurationData("Key1", "Value1");
        var settings = builder.Build();

        // Assert
        Assert.Single(settings.ConfigurationData);
        Assert.Equal("Value1", settings.ConfigurationData["Key1"]);
    }

    [Fact(DisplayName = "WithConfigurationData with dictionary adds multiple entries")]
    public void WithConfigurationData_WithDictionary_AddsMultipleEntries()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell");
        var configData = new Dictionary<string, object>
        {
            ["Key1"] = "Value1",
            ["Key2"] = "Value2"
        };

        // Act
        builder.WithConfigurationData(configData);
        var settings = builder.Build();

        // Assert
        Assert.Equal(2, settings.ConfigurationData.Count);
        Assert.Equal("Value1", settings.ConfigurationData["Key1"]);
        Assert.Equal("Value2", settings.ConfigurationData["Key2"]);
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

    [Fact(DisplayName = "FromConfiguration with IConfigurationSection merges properties with precedence")]
    public void FromConfiguration_MergesProperties_WithPrecedence()
    {
        // Arrange
        var json = @"{
            ""Shell"": {
                ""Name"": ""TestShell"",
                ""Properties"": {
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
            .WithProperty("Key1", "OriginalValue1");

        // Act
        builder.FromConfiguration(config.GetSection("Shell"));
        var settings = builder.Build();

        // Assert - Configuration should take precedence
        Assert.Equal(2, settings.Properties.Count);
        var key1Value = Assert.IsType<JsonElement>(settings.Properties["Key1"]);
        Assert.Equal("NewValue1", key1Value.GetString());
        var key2Value = Assert.IsType<JsonElement>(settings.Properties["Key2"]);
        Assert.Equal("Value2", key2Value.GetString());
    }

    [Fact(DisplayName = "FromConfiguration with IConfigurationSection loads nested settings into ConfigurationData")]
    public void FromConfiguration_LoadsNestedSettings_IntoConfigurationData()
    {
        // Arrange
        var json = @"{
            ""Shell"": {
                ""Name"": ""TestShell"",
                ""Settings"": {
                    ""Database"": {
                        ""ConnectionString"": ""Server=localhost"",
                        ""Timeout"": ""30""
                    },
                    ""SimpleValue"": ""Test""
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

        // Assert - Nested settings should be flattened
        Assert.Equal(3, settings.ConfigurationData.Count);
        Assert.Equal("Server=localhost", settings.ConfigurationData["Database:ConnectionString"]);
        Assert.Equal("30", settings.ConfigurationData["Database:Timeout"]);
        Assert.Equal("Test", settings.ConfigurationData["SimpleValue"]);
    }

    [Fact(DisplayName = "FromConfiguration with ShellConfig merges all data")]
    public void FromConfiguration_WithShellConfig_MergesAllData()
    {
        // Arrange
        var config = new ShellConfig
        {
            Name = "TestShell",
            Features = ["Feature2", "Feature3"],
            Properties = new Dictionary<string, object?>
            {
                ["Key1"] = "NewValue1",
                ["Key2"] = "Value2"
            },
            Settings = new Dictionary<string, object?>
            {
                ["Setting1"] = "SettingValue1",
                ["Setting2"] = "SettingValue2"
            }
        };

        var builder = new ShellBuilder("TestShell")
            .WithFeatures("Feature1", "Feature2")
            .WithProperty("Key1", "OriginalValue1");

        // Act
        builder.FromConfiguration(config);
        var settings = builder.Build();

        // Assert - Features merged, properties and settings loaded
        Assert.Equal(["Feature1", "Feature2", "Feature3"], settings.EnabledFeatures);
        Assert.Equal(2, settings.Properties.Count);
        Assert.Equal(2, settings.ConfigurationData.Count);
        Assert.Equal("SettingValue1", settings.ConfigurationData["Setting1"]);
        Assert.Equal("SettingValue2", settings.ConfigurationData["Setting2"]);
    }

    [Fact(DisplayName = "FromConfiguration with ShellConfig ignores null settings")]
    public void FromConfiguration_WithShellConfig_IgnoresNullSettings()
    {
        // Arrange
        var config = new ShellConfig
        {
            Name = "TestShell",
            Settings = new Dictionary<string, object?>
            {
                ["Setting1"] = "Value1",
                ["Setting2"] = null,
                ["Setting3"] = "Value3"
            }
        };

        var builder = new ShellBuilder("TestShell");

        // Act
        builder.FromConfiguration(config);
        var settings = builder.Build();

        // Assert - Null settings should be filtered out
        Assert.Equal(2, settings.ConfigurationData.Count);
        Assert.Equal("Value1", settings.ConfigurationData["Setting1"]);
        Assert.Equal("Value3", settings.ConfigurationData["Setting3"]);
        Assert.False(settings.ConfigurationData.ContainsKey("Setting2"));
    }

    [Fact(DisplayName = "Multiple FromConfiguration calls accumulate features")]
    public void MultipleFromConfiguration_AccumulatesFeatures()
    {
        // Arrange
        var config1 = new ShellConfig
        {
            Name = "TestShell",
            Features = ["Feature1", "Feature2"]
        };

        var config2 = new ShellConfig
        {
            Name = "TestShell",
            Features = ["Feature2", "Feature3"]
        };

        var builder = new ShellBuilder("TestShell");

        // Act
        builder.FromConfiguration(config1)
               .FromConfiguration(config2);
        var settings = builder.Build();

        // Assert - Features should be merged and deduplicated
        Assert.Equal(["Feature1", "Feature2", "Feature3"], settings.EnabledFeatures);
    }

    [Fact(DisplayName = "FromConfiguration handles complex property objects")]
    public void FromConfiguration_HandlesComplexPropertyObjects()
    {
        // Arrange
        var json = @"{
            ""Shell"": {
                ""Name"": ""TestShell"",
                ""Properties"": {
                    ""ComplexObject"": {
                        ""Nested"": {
                            ""Value"": ""NestedValue""
                        },
                        ""Array"": [ 1, 2, 3 ]
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

        // Assert
        Assert.Single(settings.Properties);
        var complexObject = Assert.IsType<JsonElement>(settings.Properties["ComplexObject"]);
        Assert.Equal(JsonValueKind.Object, complexObject.ValueKind);
        Assert.Equal("NestedValue", complexObject.GetProperty("Nested").GetProperty("Value").GetString());
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

    [Fact(DisplayName = "Guard clauses throw ArgumentNullException")]
    public void GuardClauses_ThrowArgumentNullException()
    {
        // Arrange
        var builder = new ShellBuilder("TestShell");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithFeatures(null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithFeature(null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithProperty(null!, "value"));
        Assert.Throws<ArgumentNullException>(() => builder.WithProperty("key", null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithProperties(null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithConfigurationData(null!, "value"));
        Assert.Throws<ArgumentNullException>(() => builder.WithConfigurationData("key", null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithConfigurationData((IDictionary<string, object>)null!));
        Assert.Throws<ArgumentNullException>(() => builder.FromConfiguration((IConfigurationSection)null!));
        Assert.Throws<ArgumentNullException>(() => builder.FromConfiguration((ShellConfig)null!));
    }
}
