using CShells.Configuration;
using Microsoft.Extensions.Configuration;

namespace CShells.Tests.Unit.Configuration;

public class ShellConfigurationTests
{
    [Fact(DisplayName = "ShellConfiguration returns shell-specific value when present")]
    public void ShellConfiguration_WithShellSpecificValue_ReturnsShellValue()
    {
        // Arrange
        var shellSettings = new ShellSettings(new ShellId("TestShell"));
        shellSettings.ConfigurationData["Theme"] = "Dark";

        var rootConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Theme"] = "Light"
            })
            .Build();

        var shellConfig = new ShellConfiguration(shellSettings, rootConfig);

        // Act
        var value = shellConfig["Theme"];

        // Assert
        Assert.Equal("Dark", value);
    }

    [Fact(DisplayName = "ShellConfiguration falls back to root value when shell value not present")]
    public void ShellConfiguration_WithoutShellSpecificValue_ReturnsRootValue()
    {
        // Arrange
        var shellSettings = new ShellSettings(new ShellId("TestShell"));

        var rootConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Theme"] = "Light",
                ["MaxUploadSizeMB"] = "10"
            })
            .Build();

        var shellConfig = new ShellConfiguration(shellSettings, rootConfig);

        // Act
        var theme = shellConfig["Theme"];
        var maxSize = shellConfig["MaxUploadSizeMB"];

        // Assert
        Assert.Equal("Light", theme);
        Assert.Equal("10", maxSize);
    }

    [Fact(DisplayName = "ShellConfiguration GetSection returns shell-specific section when present")]
    public void ShellConfiguration_GetSection_ReturnsShellSpecificSection()
    {
        // Arrange
        var shellSettings = new ShellSettings(new ShellId("TestShell"));
        shellSettings.ConfigurationData["Database:ConnectionString"] = "Server=shell-db;";
        shellSettings.ConfigurationData["Database:Timeout"] = "30";

        var rootConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Server=root-db;",
                ["Database:Timeout"] = "60"
            })
            .Build();

        var shellConfig = new ShellConfiguration(shellSettings, rootConfig);

        // Act
        var section = shellConfig.GetSection("Database");
        var connectionString = section["ConnectionString"];
        var timeout = section["Timeout"];

        // Assert
        Assert.Equal("Server=shell-db;", connectionString);
        Assert.Equal("30", timeout);
    }

    [Fact(DisplayName = "ShellConfiguration GetSection falls back to root section")]
    public void ShellConfiguration_GetSection_FallsBackToRootSection()
    {
        // Arrange
        var shellSettings = new ShellSettings(new ShellId("TestShell"));

        var rootConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Server=root-db;",
                ["Database:Timeout"] = "60"
            })
            .Build();

        var shellConfig = new ShellConfiguration(shellSettings, rootConfig);

        // Act
        var section = shellConfig.GetSection("Database");
        var connectionString = section["ConnectionString"];
        var timeout = section["Timeout"];

        // Assert
        Assert.Equal("Server=root-db;", connectionString);
        Assert.Equal("60", timeout);
    }

    [Fact(DisplayName = "ShellConfiguration can be bound to strongly-typed options")]
    public void ShellConfiguration_CanBindToStronglyTypedOptions()
    {
        // Arrange
        var shellSettings = new ShellSettings(new ShellId("TestShell"));
        shellSettings.ConfigurationData["Theme"] = "Dark";
        shellSettings.ConfigurationData["MaxUploadSizeMB"] = "50";

        var rootConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Theme"] = "Light",
                ["MaxUploadSizeMB"] = "10"
            })
            .Build();

        var shellConfig = new ShellConfiguration(shellSettings, rootConfig);

        // Act
        var options = shellConfig.Get<TestOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal("Dark", options.Theme);
        Assert.Equal(50, options.MaxUploadSizeMB);
    }

    [Fact(DisplayName = "ShellConfiguration GetChildren merges shell and root children")]
    public void ShellConfiguration_GetChildren_MergesShellAndRootChildren()
    {
        // Arrange
        var shellSettings = new ShellSettings(new ShellId("TestShell"));
        shellSettings.ConfigurationData["Feature1:Enabled"] = "true";

        var rootConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Feature1:Enabled"] = "false",
                ["Feature2:Enabled"] = "true"
            })
            .Build();

        var shellConfig = new ShellConfiguration(shellSettings, rootConfig);

        // Act
        var children = shellConfig.GetChildren().ToList();

        // Assert - should have Feature1 (from shell) and Feature2 (from root)
        Assert.Equal(2, children.Count);
        Assert.Contains(children, c => c.Key == "Feature1");
        Assert.Contains(children, c => c.Key == "Feature2");

        // Feature1 should use shell value
        var feature1 = children.First(c => c.Key == "Feature1");
        Assert.Equal("true", feature1["Enabled"]);
    }

    [Fact(DisplayName = "ShellConfiguration with empty ConfigurationData returns root values")]
    public void ShellConfiguration_WithEmptyConfigurationData_ReturnsRootValues()
    {
        // Arrange
        var shellSettings = new ShellSettings(new ShellId("TestShell"));

        var rootConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Setting1"] = "Value1",
                ["Setting2"] = "Value2"
            })
            .Build();

        var shellConfig = new ShellConfiguration(shellSettings, rootConfig);

        // Act & Assert
        Assert.Equal("Value1", shellConfig["Setting1"]);
        Assert.Equal("Value2", shellConfig["Setting2"]);
    }

    [Fact(DisplayName = "ShellConfiguration indexer set throws NotSupportedException")]
    public void ShellConfiguration_IndexerSet_ThrowsNotSupportedException()
    {
        // Arrange
        var shellSettings = new ShellSettings(new ShellId("TestShell"));
        var rootConfig = new ConfigurationBuilder().Build();
        var shellConfig = new ShellConfiguration(shellSettings, rootConfig);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => shellConfig["Key"] = "Value");
    }

    private class TestOptions
    {
        public string Theme { get; set; } = string.Empty;
        public int MaxUploadSizeMB { get; set; }
    }
}
