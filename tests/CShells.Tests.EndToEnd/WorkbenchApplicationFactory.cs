using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace CShells.Tests.EndToEnd;

/// <summary>
/// Custom WebApplicationFactory that ensures the Workbench app's content root
/// is properly configured for testing.
/// </summary>
public class WorkbenchApplicationFactory : WebApplicationFactory<Program>
{
    public WorkbenchApplicationFactory()
    {
        // Force load the Workbench assembly to ensure feature discovery works
        // AppDomain.CurrentDomain.GetAssemblies() needs to see this assembly
        _ = typeof(Program).Assembly;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set the content root to the Workbench project directory
        // This ensures the Shells folder is found during testing
        var projectDir = GetProjectPath();
        builder.UseContentRoot(projectDir);

        base.ConfigureWebHost(builder);
    }

    private static string GetProjectPath()
    {
        // Get the path to the test project
        var testProjectPath = AppContext.BaseDirectory;

        // Navigate up to find the solution root, then down to the Workbench project
        var directory = new DirectoryInfo(testProjectPath);

        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CShells.sln")))
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            throw new InvalidOperationException("Could not find solution root directory");
        }

        var workbenchPath = Path.Combine(directory.FullName, "samples", "CShells.Workbench");

        if (!Directory.Exists(workbenchPath))
        {
            throw new InvalidOperationException($"Workbench project not found at: {workbenchPath}");
        }

        return workbenchPath;
    }
}
