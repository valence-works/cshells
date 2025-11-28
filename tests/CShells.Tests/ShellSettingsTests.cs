namespace CShells.Tests;

public class ShellSettingsTests
{
    private const string TestShellName = "TestShell";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constructor_InitializesWithEmptyCollections(bool withShellId)
    {
        // Arrange & Act
        var settings = withShellId ? new(CreateTestShellId()) : new ShellSettings();

        // Assert
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

    [Theory]
    [InlineData("Key1", "Value1")]
    [InlineData("Key2", 42)]
    public void Properties_CanAddAndRetrieveValues(string key, object value)
    {
        // Arrange
        var settings = new ShellSettings(CreateTestShellId());

        // Act
        settings.Properties[key] = value;

        // Assert
        Assert.Equal(value, settings.Properties[key]);
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
