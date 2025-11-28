namespace CShells.Tests.Unit;

public class ShellFeatureDescriptorTests
{
    private const string TestFeatureId = "TestFeature";

    [Fact(DisplayName = "Default constructor initializes with default values")]
    public void DefaultConstructor_InitializesWithDefaults()
    {
        // Act
        var descriptor = new ShellFeatureDescriptor();

        // Assert
        Assert.Equal(string.Empty, descriptor.Id);
        Assert.NotNull(descriptor.Dependencies);
        Assert.Empty(descriptor.Dependencies);
        Assert.NotNull(descriptor.Metadata);
        Assert.Empty(descriptor.Metadata);
        Assert.Null(descriptor.StartupType);
    }

    [Fact(DisplayName = "Constructor with ID sets ID and initializes defaults")]
    public void Constructor_WithId_SetsIdAndInitializesDefaults()
    {
        // Act
        var descriptor = new ShellFeatureDescriptor(TestFeatureId);

        // Assert
        Assert.Equal(TestFeatureId, descriptor.Id);
        Assert.NotNull(descriptor.Dependencies);
        Assert.Empty(descriptor.Dependencies);
        Assert.NotNull(descriptor.Metadata);
        Assert.Empty(descriptor.Metadata);
        Assert.Null(descriptor.StartupType);
    }

    [Fact(DisplayName = "Constructor with null ID throws ArgumentNullException")]
    public void Constructor_WithNullId_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new ShellFeatureDescriptor(null!));
        Assert.Equal("id", ex.ParamName);
    }

    [Fact(DisplayName = "Dependencies property can be set and retrieved")]
    public void Dependencies_CanBeSet()
    {
        // Arrange
        var descriptor = new ShellFeatureDescriptor(TestFeatureId);
        var dependencies = new[] { "Dependency1", "Dependency2" };

        // Act
        descriptor.Dependencies = dependencies;

        // Assert
        Assert.Equal(dependencies, descriptor.Dependencies);
    }

    [Fact(DisplayName = "Metadata dictionary can store and retrieve values")]
    public void Metadata_CanBeSetAndRetrieveValues()
    {
        // Arrange
        var descriptor = new ShellFeatureDescriptor(TestFeatureId)
        {
            Metadata =
            {
                // Act
                ["Key1"] = "Value1",
                ["Key2"] = 42
            }
        };

        // Assert
        Assert.Equal("Value1", descriptor.Metadata["Key1"]);
        Assert.Equal(42, descriptor.Metadata["Key2"]);
    }
}
