using CShells.Configuration;

namespace CShells.Tests.Unit;

public class ShellSettingsFactoryTests
{
    [Fact(DisplayName = "Create with null config throws ArgumentNullException")]
    public void Create_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => ShellSettingsFactory.Create(null!));
        Assert.Equal("config", ex.ParamName);
    }

    [Fact(DisplayName = "Create with valid config returns ShellSettings")]
    public void Create_WithValidConfig_ReturnsShellSettings()
    {
        // Arrange
        var config = new ShellConfig
        {
            Name = "TestShell",
            Features = ["Feature1", "Feature2"],
            Properties = new()
            {
                ["Key1"] = "Value1"
            }
        };

        // Act
        var settings = ShellSettingsFactory.Create(config);

        // Assert
        Assert.Equal("TestShell", settings.Id.Name);
        Assert.Equal(["Feature1", "Feature2"], settings.EnabledFeatures);
        Assert.Single(settings.Properties);
        Assert.Equal("Value1", settings.Properties["Key1"]);
    }

    [Fact(DisplayName = "Create with empty config returns ShellSettings with empty collections")]
    public void Create_WithEmptyConfig_ReturnsShellSettingsWithEmptyCollections()
    {
        // Arrange
        var config = new ShellConfig { Name = "EmptyShell" };

        // Act
        var settings = ShellSettingsFactory.Create(config);

        // Assert
        Assert.Equal("EmptyShell", settings.Id.Name);
        Assert.Empty(settings.EnabledFeatures);
        Assert.Empty(settings.Properties);
    }

    [Fact(DisplayName = "CreateAll with null options throws ArgumentNullException")]
    public void CreateAll_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => ShellSettingsFactory.CreateAll(null!));
        Assert.Equal("options", ex.ParamName);
    }

    [Fact(DisplayName = "CreateAll with valid options returns ShellSettings collection")]
    public void CreateAll_WithValidOptions_ReturnsShellSettingsCollection()
    {
        // Arrange
        var options = new CShellsOptions
        {
            Shells =
            [
                new() { Name = "Shell1", Features = ["Feature1"] },
                new() { Name = "Shell2", Features = ["Feature2", "Feature3"] }
            ]
        };

        // Act
        var settingsList = ShellSettingsFactory.CreateAll(options);

        // Assert
        Assert.Equal(2, settingsList.Count);
        Assert.Equal("Shell1", settingsList[0].Id.Name);
        Assert.Equal(["Feature1"], settingsList[0].EnabledFeatures);
        Assert.Equal("Shell2", settingsList[1].Id.Name);
        Assert.Equal(["Feature2", "Feature3"], settingsList[1].EnabledFeatures);
    }

    [Fact(DisplayName = "CreateAll with empty shells returns empty collection")]
    public void CreateAll_WithEmptyShells_ReturnsEmptyCollection()
    {
        // Arrange
        var options = new CShellsOptions();

        // Act
        var settingsList = ShellSettingsFactory.CreateAll(options);

        // Assert
        Assert.Empty(settingsList);
    }

    [Fact(DisplayName = "Create normalizes feature names by trimming whitespace and filtering nulls")]
    public void Create_NormalizesFeatureNames()
    {
        // Arrange
        var config = new ShellConfig
        {
            Name = "TestShell",
            Features = [" Feature1 ", "Feature2", "  ", null, "Feature3  "]
        };

        // Act
        var settings = ShellSettingsFactory.Create(config);

        // Assert
        Assert.Equal(["Feature1", "Feature2", "Feature3"], settings.EnabledFeatures);
    }

    [Fact(DisplayName = "CreateAll with duplicate shell names throws ArgumentException")]
    public void CreateAll_WithDuplicateShellNames_ThrowsArgumentException()
    {
        // Arrange
        var options = new CShellsOptions
        {
            Shells =
            [
                new() { Name = "DuplicateShell" },
                new() { Name = "DuplicateShell" }
            ]
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => ShellSettingsFactory.CreateAll(options));
        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
