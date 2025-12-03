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
        Assert.True(defaultShell.Settings.Properties.ContainsKey("WebRouting"));
        Assert.True(acmeShell.Settings.Properties.ContainsKey("WebRouting"));
        Assert.True(contosoShell.Settings.Properties.ContainsKey("WebRouting"));

        // Properties are JsonElement objects, deserialize to WebRoutingShellOptions
        var defaultWebRouting = GetWebRoutingOptions(defaultShell.Settings.Properties["WebRouting"]);
        var acmeWebRouting = GetWebRoutingOptions(acmeShell.Settings.Properties["WebRouting"]);
        var contosoWebRouting = GetWebRoutingOptions(contosoShell.Settings.Properties["WebRouting"]);

        Assert.NotNull(defaultWebRouting);
        Assert.NotNull(acmeWebRouting);
        Assert.NotNull(contosoWebRouting);

        // Configuration system may convert empty strings to null in nested objects
        Assert.True(string.IsNullOrEmpty(defaultWebRouting.Path), $"Expected empty or null path for Default shell, got: '{defaultWebRouting.Path}'");
        Assert.Equal("acme", acmeWebRouting.Path);
        Assert.Equal("contoso", contosoWebRouting.Path);
    }

    private static WebRoutingShellOptions? GetWebRoutingOptions(object value)
    {
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return System.Text.Json.JsonSerializer.Deserialize<WebRoutingShellOptions>(jsonElement.GetRawText());
        }
        return null;
    }
}
