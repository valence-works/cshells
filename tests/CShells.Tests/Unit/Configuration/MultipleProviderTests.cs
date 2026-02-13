using CShells.Configuration;
using CShells.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Unit.Configuration;

public class MultipleProviderTests
{
    [Fact]
    public async Task CodeFirstShells_AreAutomaticallyRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddCShells(shells =>
        {
            shells.AddShell("Shell1", shell => shell.WithFeatures("Feature1"));
            shells.AddShell("Shell2", shell => shell.WithFeatures("Feature2"));
        });
        
        var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IShellSettingsProvider>();
        
        // Assert
        var settings = (await provider.GetShellSettingsAsync()).ToList();
        Assert.Equal(2, settings.Count);
        Assert.Contains(settings, s => s.Id.Name == "Shell1");
        Assert.Contains(settings, s => s.Id.Name == "Shell2");
    }
    
    [Fact]
    public async Task MultipleProviders_AreMerged()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider1Shells = new List<ShellSettings>
        {
            new(new ShellId("Provider1Shell"), ["Feature1"])
        };
        var provider2Shells = new List<ShellSettings>
        {
            new(new ShellId("Provider2Shell"), ["Feature2"])
        };
        
        // Act
        services.AddCShells(shells =>
        {
            shells.WithProvider(new InMemoryShellSettingsProvider(provider1Shells));
            shells.WithProvider(new InMemoryShellSettingsProvider(provider2Shells));
        });
        
        var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IShellSettingsProvider>();
        
        // Assert
        var settings = (await provider.GetShellSettingsAsync()).ToList();
        Assert.Equal(2, settings.Count);
        Assert.Contains(settings, s => s.Id.Name == "Provider1Shell");
        Assert.Contains(settings, s => s.Id.Name == "Provider2Shell");
    }
    
    [Fact]
    public async Task LaterProviders_OverrideEarlierOnes()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider1Shells = new List<ShellSettings>
        {
            new(new ShellId("SharedShell"), ["Feature1"])
        };
        var provider2Shells = new List<ShellSettings>
        {
            new(new ShellId("SharedShell"), ["Feature2"])
        };
        
        // Act
        services.AddCShells(shells =>
        {
            shells.WithProvider(new InMemoryShellSettingsProvider(provider1Shells));
            shells.WithProvider(new InMemoryShellSettingsProvider(provider2Shells));
        });
        
        var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IShellSettingsProvider>();
        
        // Assert
        var settings = (await provider.GetShellSettingsAsync()).ToList();
        Assert.Single(settings);
        var shell = settings.First();
        Assert.Equal("SharedShell", shell.Id.Name);
        Assert.Equal(["Feature2"], shell.EnabledFeatures); // Feature2 from provider2, not Feature1
    }
    
    [Fact]
    public async Task CodeFirstAndProviders_WorkTogether()
    {
        // Arrange
        var services = new ServiceCollection();
        var providerShells = new List<ShellSettings>
        {
            new(new ShellId("ProviderShell"), ["ProviderFeature"])
        };
        
        // Act
        services.AddCShells(shells =>
        {
            shells.AddShell("CodeFirstShell", shell => shell.WithFeatures("CodeFirstFeature"));
            shells.WithProvider(new InMemoryShellSettingsProvider(providerShells));
        });
        
        var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IShellSettingsProvider>();
        
        // Assert
        var settings = (await provider.GetShellSettingsAsync()).ToList();
        Assert.Equal(2, settings.Count);
        Assert.Contains(settings, s => s.Id.Name == "CodeFirstShell");
        Assert.Contains(settings, s => s.Id.Name == "ProviderShell");
    }
    
    [Fact]
    public async Task NoProvidersOrShells_ReturnsEmptyProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddCShells(_ =>
        {
            // No shells or providers registered
        });
        
        var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IShellSettingsProvider>();
        
        // Assert
        var settings = (await provider.GetShellSettingsAsync()).ToList();
        Assert.Empty(settings);
    }
    
    [Fact]
    public async Task MutableProvider_SupportsRuntimeChanges()
    {
        // Arrange
        var mutableProvider = new MutableInMemoryShellSettingsProvider();
        
        // Act - Add shell
        var shell1 = new ShellSettings(new ShellId("Shell1"), ["Feature1"]);
        mutableProvider.AddOrUpdate(shell1);
        
        var settings1 = (await mutableProvider.GetShellSettingsAsync()).ToList();
        
        // Act - Add another shell
        var shell2 = new ShellSettings(new ShellId("Shell2"), ["Feature2"]);
        mutableProvider.AddOrUpdate(shell2);
        
        var settings2 = (await mutableProvider.GetShellSettingsAsync()).ToList();
        
        // Act - Remove first shell
        mutableProvider.Remove(new ShellId("Shell1"));
        
        var settings3 = (await mutableProvider.GetShellSettingsAsync()).ToList();
        
        // Assert
        Assert.Single(settings1);
        Assert.Equal(2, settings2.Count);
        Assert.Single(settings3);
        Assert.Equal("Shell2", settings3.First().Id.Name);
    }
}

