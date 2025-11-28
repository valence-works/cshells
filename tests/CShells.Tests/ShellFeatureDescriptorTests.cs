namespace CShells.Tests;

public class ShellFeatureDescriptorTests
{
    private const string TestFeatureId = "TestFeature";

    [Theory]
    [InlineData(null, "")]
    [InlineData(TestFeatureId, TestFeatureId)]
    public void Constructor_InitializesWithDefaultsAndSetsId(string? id, string expectedId)
    {
        // Act
        var descriptor = id == null ? new() : new ShellFeatureDescriptor(id);

        // Assert
        Assert.Equal(expectedId, descriptor.Id);
        Assert.NotNull(descriptor.Dependencies);
        Assert.Empty(descriptor.Dependencies);
        Assert.NotNull(descriptor.Metadata);
        Assert.Empty(descriptor.Metadata);
        Assert.Null(descriptor.StartupType);
    }

    [Fact]
    public void Id_CanBeSet()
    {
        // Arrange
        var descriptor = new ShellFeatureDescriptor();

        // Act
        descriptor.Id = TestFeatureId;

        // Assert
        Assert.Equal(TestFeatureId, descriptor.Id);
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

    [Fact]
    public void StartupType_CanBeSet()
    {
        // Arrange
        var descriptor = new ShellFeatureDescriptor(TestFeatureId);

        // Act
        descriptor.StartupType = typeof(TestStartupClass);

        // Assert
        Assert.Equal(typeof(TestStartupClass), descriptor.StartupType);
    }

    private class TestStartupClass
    {
    }
}
