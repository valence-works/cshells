namespace CShells.Tests;

public class ShellSettingsTests
{
    private const string TestShellName = "TestShell";

    [Fact]
    public void DefaultConstructor_InitializesWithEmptyCollections()
    {
        // Act
        var settings = new ShellSettings();

        // Assert
        AssertHasEmptyCollections(settings);
    }

    [Fact]
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

    [Fact]
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

    [Fact]
    public void Constructor_WithNullFeatures_ThrowsArgumentNullException()
    {
        // Arrange
        var shellId = CreateTestShellId();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new ShellSettings(shellId, null!));
        Assert.Equal("enabledFeatures", ex.ParamName);
    }

    [Fact]
    public void Properties_CanAddAndRetrieveValues()
    {
        // Arrange
        var settings = new ShellSettings(CreateTestShellId());

        // Act
        settings.Properties["Key1"] = "Value1";
        settings.Properties["Key2"] = 42;

        // Assert
        Assert.Equal("Value1", settings.Properties["Key1"]);
        Assert.Equal(42, settings.Properties["Key2"]);
    }

    [Fact]
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

    [Fact]
    public void EnabledFeatures_CreatesDefensiveCopy()
    {
        // Arrange
        var settings = new ShellSettings();
        var features = CreateTestFeatures();

        // Act
        settings.EnabledFeatures = features;
        features.Add("Feature3");

        // Assert
        Assert.Equal(2, settings.EnabledFeatures.Count);
        Assert.DoesNotContain("Feature3", settings.EnabledFeatures);
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
