using CShells.Configuration;

namespace CShells.Tests.Unit;

public class CShellsOptionsTests
{
    [Fact(DisplayName = "SectionName is 'CShells'")]
    public void SectionName_IsCShells()
    {
        // Assert
        Assert.Equal("CShells", CShellsOptions.SectionName);
    }

    [Fact(DisplayName = "Default constructor initializes with empty shells list")]
    public void DefaultConstructor_InitializesWithEmptyShellsList()
    {
        // Act
        var options = new CShellsOptions();

        // Assert
        Assert.NotNull(options.Shells);
        Assert.Empty(options.Shells);
    }

    [Fact(DisplayName = "Shells can be added and retrieved")]
    public void Shells_CanBeAddedAndRetrieved()
    {
        // Arrange
        var options = new CShellsOptions
        {
            Shells =
            [
                new() { Name = "Shell1" },
                new() { Name = "Shell2" }
            ]
        };

        // Assert
        Assert.Equal(2, options.Shells.Count);
        Assert.Equal("Shell1", options.Shells[0].Name);
        Assert.Equal("Shell2", options.Shells[1].Name);
    }
}
