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

    [Fact(DisplayName = "Shell properties contain path mappings")]
    public void ShellProperties_ContainPathMappings()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var shellHost = scope.ServiceProvider.GetRequiredService<IShellHost>();

        // Act
        var defaultShell = shellHost.AllShells.First(s => s.Id.Name == "Default");
        var acmeShell = shellHost.AllShells.First(s => s.Id.Name == "Acme");
        var contosoShell = shellHost.AllShells.First(s => s.Id.Name == "Contoso");

        // Assert
        Assert.True(defaultShell.Settings.Properties.ContainsKey("AspNetCore.Path"));
        Assert.True(acmeShell.Settings.Properties.ContainsKey("AspNetCore.Path"));
        Assert.True(contosoShell.Settings.Properties.ContainsKey("AspNetCore.Path"));

        // Properties may be JsonElement objects from deserialization, extract string values
        var defaultPath = GetPropertyAsString(defaultShell.Settings.Properties["AspNetCore.Path"]);
        var acmePath = GetPropertyAsString(acmeShell.Settings.Properties["AspNetCore.Path"]);
        var contosoPath = GetPropertyAsString(contosoShell.Settings.Properties["AspNetCore.Path"]);

        Assert.Equal("", defaultPath);
        Assert.Equal("acme", acmePath);
        Assert.Equal("contoso", contosoPath);
    }

    private static string? GetPropertyAsString(object value)
    {
        return value switch
        {
            string s => s,
            System.Text.Json.JsonElement jsonElement when jsonElement.ValueKind == System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
            _ => null
        };
    }
}
