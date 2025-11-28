namespace CShells.Tests.Integration.DefaultShellHost;

/// <summary>
/// Tests for <see cref="CShells.DefaultShellHost"/> lifecycle operations (Dispose).
/// </summary>
public class LifecycleTests
{
    [Fact(DisplayName = "Dispose disposes all service providers")]
    public void Dispose_DisposesServiceProviders()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new("TestShell"))
        };
        var host = new CShells.DefaultShellHost(settings, []);
        _ = host.GetShell(new("TestShell")); // Ensure the shell is built

        // Act
        host.Dispose();

        // Assert - After dispose, accessing shells should throw
        Assert.Throws<ObjectDisposedException>(() => host.DefaultShell);
        Assert.Throws<ObjectDisposedException>(() => host.GetShell(new("TestShell")));
        Assert.Throws<ObjectDisposedException>(() => _ = host.AllShells);
    }
}
