using CShells.Configuration;

namespace CShells.Tests.Unit;

public class ShellConfigTests
{
    [Fact(DisplayName = "Default constructor initializes with empty values")]
    public void DefaultConstructor_InitializesWithEmptyValues()
    {
        // Act
        var config = new ShellConfig();

        // Assert
        Assert.Equal(string.Empty, config.Name);
        Assert.NotNull(config.Features);
        Assert.Empty(config.Features);
        Assert.NotNull(config.Properties);
        Assert.Empty(config.Properties);
    }

    [Fact(DisplayName = "Properties can be set and retrieved")]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var config = new ShellConfig
        {
            Name = "TestShell",
            Features = ["Feature1", "Feature2"],
            Properties = new()
            {
                ["Key1"] = "Value1",
                ["Key2"] = 42
            }
        };

        // Assert
        Assert.Equal("TestShell", config.Name);
        Assert.Equal(2, config.Features.Length);
        Assert.Equal("Feature1", config.Features[0]);
        Assert.Equal("Feature2", config.Features[1]);
        Assert.Equal(2, config.Properties.Count);
        Assert.Equal("Value1", config.Properties["Key1"]);
        Assert.Equal(42, config.Properties["Key2"]);
    }
}
