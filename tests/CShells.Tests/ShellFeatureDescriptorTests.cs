namespace CShells.Tests;

public class ShellFeatureDescriptorTests
{
    private const string TestFeatureId = "TestFeature";

    [Fact]
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

    [Fact]
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

    [Fact]
    public void Constructor_WithNullId_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new ShellFeatureDescriptor(null!));
        Assert.Equal("id", ex.ParamName);
    }

    [Fact]
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

    [Fact]
    public void Metadata_CanBeSetAndRetrieveValues()
    {
        // Arrange
        var descriptor = new ShellFeatureDescriptor(TestFeatureId);

        // Act
        descriptor.Metadata["Key1"] = "Value1";
        descriptor.Metadata["Key2"] = 42;

        // Assert
        Assert.Equal("Value1", descriptor.Metadata["Key1"]);
        Assert.Equal(42, descriptor.Metadata["Key2"]);
    }
}
