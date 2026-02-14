using CShells.AspNetCore;
using CShells.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.EndToEnd;

/// <summary>
/// Tests to verify shell configuration is loaded correctly from JSON files.
/// </summary>
[Collection("Workbench")]
public class ShellConfigurationTests(WorkbenchApplicationFactory factory)
{
    [Fact(DisplayName = "All three shells are loaded")]
    public void AllShells_AreLoaded()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var shellHost = scope.ServiceProvider.GetRequiredService<IShellHost>();

        // Act
        var shells = shellHost.AllShells.ToList();

        // Assert
        Assert.Equal(3, shells.Count);
        Assert.Contains(shells, s => s.Id.Name == "Default");
        Assert.Contains(shells, s => s.Id.Name == "Acme");
        Assert.Contains(shells, s => s.Id.Name == "Contoso");
    }

    [Fact(DisplayName = "Shell configuration contains WebRouting path mappings")]
    public void ShellConfiguration_ContainsPathMappings()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var shellHost = scope.ServiceProvider.GetRequiredService<IShellHost>();

        // Act
        var defaultShell = shellHost.AllShells.First(s => s.Id.Name == "Default");
        var acmeShell = shellHost.AllShells.First(s => s.Id.Name == "Acme");
        var contosoShell = shellHost.AllShells.First(s => s.Id.Name == "Contoso");

        // Assert - Configuration is now flattened to ConfigurationData
        Assert.True(defaultShell.Settings.ConfigurationData.ContainsKey("WebRouting:Path"));
        Assert.True(acmeShell.Settings.ConfigurationData.ContainsKey("WebRouting:Path"));
        Assert.True(contosoShell.Settings.ConfigurationData.ContainsKey("WebRouting:Path"));

        var defaultPath = defaultShell.Settings.GetConfiguration("WebRouting:Path");
        var acmePath = acmeShell.Settings.GetConfiguration("WebRouting:Path");
        var contosoPath = contosoShell.Settings.GetConfiguration("WebRouting:Path");

        // Configuration system may convert empty strings to null or empty
        Assert.True(string.IsNullOrEmpty(defaultPath), $"Expected empty or null path for Default shell, got: '{defaultPath}'");
        Assert.Equal("acme", acmePath);
        Assert.Equal("contoso", contosoPath);
    }
}
