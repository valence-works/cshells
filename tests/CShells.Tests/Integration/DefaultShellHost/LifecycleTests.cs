using CShells.Hosting;
using CShells.Tests.Integration.ShellHost;
using CShells.Tests.TestHelpers;

namespace CShells.Tests.Integration.DefaultShellHost;

/// <summary>
/// Tests for <see cref="DefaultShellHost"/> lifecycle operations (Dispose).
/// </summary>
[Collection(nameof(DefaultShellHostCollection))]
public class LifecycleTests(DefaultShellHostFixture fixture)
{
    [Fact(DisplayName = "Dispose disposes all service providers")]
    public void Dispose_DisposesServiceProviders()
    {
        // Arrange
        var host = fixture.CreateHost([new(new("TestShell"))], typeof(TestFixtures).Assembly);
        _ = host.GetShell(new("TestShell")); // Ensure the shell is built

        // Act
        host.Dispose();

        // Assert - After dispose, accessing shells should throw
        Assert.Throws<ObjectDisposedException>(() => host.DefaultShell);
        Assert.Throws<ObjectDisposedException>(() => host.GetShell(new("TestShell")));
        Assert.Throws<ObjectDisposedException>(() => _ = host.AllShells);
    }
}
