namespace CShells.Tests.Unit;

public class ShellSettingsTests
{
    private const string TestShellName = "TestShell";

    [Fact(DisplayName = "Default constructor initializes with empty collections")]
    public void DefaultConstructor_InitializesWithEmptyCollections()
    {
        // Act
        var settings = new ShellSettings();

        // Assert
        AssertHasEmptyCollections(settings);
    }

    [Fact(DisplayName = "Constructor with ShellId sets ID property")]
    public void Constructor_WithShellId_SetsId()
    {
        // Arrange
        var shellId = CreateTestShellId();

        // Act
        var settings = new ShellSettings(shellId);

        // Assert
        Assert.Equal(shellId, settings.Id);
        AssertHasEmptyCollections(settings);
    }

    [Fact(DisplayName = "Constructor with ShellId and features sets all properties")]
    public void Constructor_WithShellIdAndFeatures_SetsProperties()
    {
        // Arrange
        var shellId = CreateTestShellId();
        var features = CreateTestFeatures();

        // Act
        var settings = new ShellSettings(shellId, features);

        // Assert
        Assert.Equal(shellId, settings.Id);
        Assert.Equal(features, settings.EnabledFeatures);
        Assert.NotNull(settings.Properties);
        Assert.Empty(settings.Properties);
    }

    [Fact(DisplayName = "Constructor with null features throws ArgumentNullException")]
    public void Constructor_WithNullFeatures_ThrowsArgumentNullException()
    {
        // Arrange
        var shellId = CreateTestShellId();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new ShellSettings(shellId, null!));
        Assert.Equal("enabledFeatures", ex.ParamName);
    }

    [Fact(DisplayName = "EnabledFeatures property can be set and retrieved")]
    public void EnabledFeatures_CanBeSet()
    {
        // Arrange
        var settings = new ShellSettings();
        List<string> features = ["Feature1", "Feature2", "Feature3"];

        // Act
        settings.EnabledFeatures = features;

        // Assert
        Assert.Equal(features, settings.EnabledFeatures);
    }

    private static ShellId CreateTestShellId() => new(TestShellName);

    private static List<string> CreateTestFeatures() => ["Feature1", "Feature2"];

    private static void AssertHasEmptyCollections(ShellSettings settings)
    {
        Assert.NotNull(settings.EnabledFeatures);
        Assert.Empty(settings.EnabledFeatures);
        Assert.NotNull(settings.Properties);
        Assert.Empty(settings.Properties);
    }
}
