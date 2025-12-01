using CShells.Configuration;
using CShells.Hosting;
using CShells.Tests.Integration.ShellHost;

namespace CShells.Tests.Integration.DefaultShellHost;

/// <summary>
/// Tests for <see cref="DefaultShellHost"/> lifecycle operations (Dispose).
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
        var cache = new ShellSettingsCache();
        cache.Load(settings);
        var (services, provider) = TestFixtures.CreateRootServices();
        var accessor = TestFixtures.CreateRootServicesAccessor(services);
        var factory = new CShells.Features.DefaultShellFeatureFactory(provider);
        var host = new Hosting.DefaultShellHost(cache, [], provider, accessor, factory);
        _ = host.GetShell(new("TestShell")); // Ensure the shell is built

        // Act
        host.Dispose();

        // Assert - After dispose, accessing shells should throw
        Assert.Throws<ObjectDisposedException>(() => host.DefaultShell);
        Assert.Throws<ObjectDisposedException>(() => host.GetShell(new("TestShell")));
        Assert.Throws<ObjectDisposedException>(() => _ = host.AllShells);
    }
}
