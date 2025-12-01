using CShells.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.EndToEnd;

/// <summary>
/// Debugging tests to understand application initialization
/// </summary>
[Collection("Workbench")]
public class DebugTests(WorkbenchApplicationFactory factory)
{
    [Fact]
    public void ApplicationFactory_IsInitialized()
    {
        // This test ensures the factory itself can initialize
        Assert.NotNull(factory);
        Assert.NotNull(factory.Services);
    }

    [Fact]
    public void ShellHost_IsRegistered()
    {
        using var scope = factory.Services.CreateScope();
        var shellHost = scope.ServiceProvider.GetService<IShellHost>();

        Assert.NotNull(shellHost);
    }

    [Fact]
    public void ShellsDirectory_IsAccessible()
    {
        // Get the content root from the factory
        var contentRoot = factory.Services.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>()?.ContentRootPath;
        Assert.NotNull(contentRoot);

        var shellsPath = Path.Combine(contentRoot!, "Shells");
        Assert.True(Directory.Exists(shellsPath), $"Shells directory not found at: {shellsPath}");

        var files = Directory.GetFiles(shellsPath, "*.json");
        Assert.NotEmpty(files);
        Assert.Contains(files, f => f.EndsWith("Default.json"));
        Assert.Contains(files, f => f.EndsWith("Acme.json"));
        Assert.Contains(files, f => f.EndsWith("Contoso.json"));
    }

}
