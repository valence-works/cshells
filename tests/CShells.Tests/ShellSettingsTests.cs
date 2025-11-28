using FluentAssertions;

namespace CShells.Tests;

public class ShellSettingsTests
{
    [Fact]
    public void DefaultConstructor_InitializesWithEmptyCollections()
    {
        // Act
        var settings = new ShellSettings();

        // Assert
        settings.EnabledFeatures.Should().NotBeNull().And.BeEmpty();
        settings.Properties.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Constructor_WithShellId_SetsId()
    {
        // Arrange
        var shellId = new ShellId("TestShell");

        // Act
        var settings = new ShellSettings(shellId);

        // Assert
        settings.Id.Should().Be(shellId);
        settings.EnabledFeatures.Should().NotBeNull().And.BeEmpty();
        settings.Properties.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Constructor_WithShellIdAndFeatures_SetsProperties()
    {
        // Arrange
        var shellId = new ShellId("TestShell");
        var features = new List<string> { "Feature1", "Feature2" };

        // Act
        var settings = new ShellSettings(shellId, features);

        // Assert
        settings.Id.Should().Be(shellId);
        settings.EnabledFeatures.Should().BeEquivalentTo(features);
        settings.Properties.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullFeatures_ThrowsArgumentNullException()
    {
        // Arrange
        var shellId = new ShellId("TestShell");

        // Act
        var act = () => new ShellSettings(shellId, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("enabledFeatures");
    }

    [Fact]
    public void Properties_CanAddAndRetrieveValues()
    {
        // Arrange
        var settings = new ShellSettings(new ShellId("TestShell"));

        // Act
        settings.Properties["Key1"] = "Value1";
        settings.Properties["Key2"] = 42;

        // Assert
        settings.Properties["Key1"].Should().Be("Value1");
        settings.Properties["Key2"].Should().Be(42);
    }

    [Fact]
    public void EnabledFeatures_CanBeSet()
    {
        // Arrange
        var settings = new ShellSettings();
        var features = new List<string> { "Feature1", "Feature2", "Feature3" };

        // Act
        settings.EnabledFeatures = features;

        // Assert
        settings.EnabledFeatures.Should().BeEquivalentTo(features);
    }
}
