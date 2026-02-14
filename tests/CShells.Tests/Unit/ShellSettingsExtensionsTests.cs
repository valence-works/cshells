namespace CShells.Tests.Unit;

public class ShellSettingsExtensionsTests
{
    [Fact(DisplayName = "GetConfiguration with string value returns string")]
    public void GetConfiguration_WithStringValue_ReturnsString()
    {
        // Arrange
        var settings = new ShellSettings
        {
            ConfigurationData = new Dictionary<string, object>
            {
                ["key"] = "value"
            }
        };

        // Act
        var result = settings.GetConfiguration("key");

        // Assert
        Assert.Equal("value", result);
    }

    [Fact(DisplayName = "GetConfiguration generic with string value returns typed value")]
    public void GetConfiguration_Generic_WithStringValue_ReturnsTypedValue()
    {
        // Arrange
        var settings = new ShellSettings
        {
            ConfigurationData = new Dictionary<string, object>
            {
                ["count"] = "42"
            }
        };

        // Act
        var result = settings.GetConfiguration<int>("count");

        // Assert
        Assert.Equal(42, result);
    }

    [Fact(DisplayName = "GetConfiguration with non-existent key returns null")]
    public void GetConfiguration_WithNonExistentKey_ReturnsNull()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act
        var result = settings.GetConfiguration("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "GetConfiguration generic with non-existent key returns default")]
    public void GetConfiguration_Generic_WithNonExistentKey_ReturnsDefault()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act
        var result = settings.GetConfiguration<int>("nonexistent");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact(DisplayName = "GetConfiguration with null settings throws ArgumentNullException")]
    public void GetConfiguration_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        ShellSettings? settings = null;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => settings!.GetConfiguration("key"));
        Assert.Equal("settings", ex.ParamName);
    }

    [Fact(DisplayName = "GetConfiguration with null key throws ArgumentException")]
    public void GetConfiguration_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => settings.GetConfiguration(null!));
    }

    [Fact(DisplayName = "GetConfiguration with whitespace key throws ArgumentException")]
    public void GetConfiguration_WithWhitespaceKey_ThrowsArgumentException()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => settings.GetConfiguration("   "));
    }

    [Fact(DisplayName = "SetConfiguration stores value in ConfigurationData")]
    public void SetConfiguration_StoresValueInConfigurationData()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act
        settings.SetConfiguration("key", "value");

        // Assert
        Assert.True(settings.ConfigurationData.ContainsKey("key"));
        Assert.Equal("value", settings.ConfigurationData["key"]);
    }

    [Fact(DisplayName = "SetConfiguration with null settings throws ArgumentNullException")]
    public void SetConfiguration_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        ShellSettings? settings = null;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => settings!.SetConfiguration("key", "value"));
        Assert.Equal("settings", ex.ParamName);
    }

    [Fact(DisplayName = "SetConfiguration with null key throws ArgumentException")]
    public void SetConfiguration_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => settings.SetConfiguration(null!, "value"));
    }

    [Fact(DisplayName = "SetConfiguration with null value throws ArgumentNullException")]
    public void SetConfiguration_WithNullValue_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => settings.SetConfiguration("key", null!));
    }

    [Fact(DisplayName = "TryGetConfiguration with existing key returns true and value")]
    public void TryGetConfiguration_WithExistingKey_ReturnsTrueAndValue()
    {
        // Arrange
        var settings = new ShellSettings
        {
            ConfigurationData = new Dictionary<string, object>
            {
                ["key"] = "value"
            }
        };

        // Act
        var result = settings.TryGetConfiguration<string>("key", out var value);

        // Assert
        Assert.True(result);
        Assert.Equal("value", value);
    }

    [Fact(DisplayName = "TryGetConfiguration with non-existent key returns false")]
    public void TryGetConfiguration_WithNonExistentKey_ReturnsFalse()
    {
        // Arrange
        var settings = new ShellSettings();

        // Act
        var result = settings.TryGetConfiguration<string>("nonexistent", out var value);

        // Assert
        Assert.False(result);
        Assert.Null(value);
    }

    [Fact(DisplayName = "TryGetConfiguration with null settings throws ArgumentNullException")]
    public void TryGetConfiguration_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        ShellSettings? settings = null;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => settings!.TryGetConfiguration<string>("key", out _));
        Assert.Equal("settings", ex.ParamName);
    }

    [Fact(DisplayName = "GetConfiguration with int value returns int")]
    public void GetConfiguration_WithIntValue_ReturnsInt()
    {
        // Arrange
        var settings = new ShellSettings
        {
            ConfigurationData = new Dictionary<string, object>
            {
                ["count"] = 42
            }
        };

        // Act
        var result = settings.GetConfiguration<int>("count");

        // Assert
        Assert.Equal(42, result);
    }

    [Fact(DisplayName = "GetConfiguration with colon-separated key works")]
    public void GetConfiguration_WithColonSeparatedKey_Works()
    {
        // Arrange
        var settings = new ShellSettings
        {
            ConfigurationData = new Dictionary<string, object>
            {
                ["WebRouting:Path"] = "acme"
            }
        };

        // Act
        var result = settings.GetConfiguration("WebRouting:Path");

        // Assert
        Assert.Equal("acme", result);
    }
}
