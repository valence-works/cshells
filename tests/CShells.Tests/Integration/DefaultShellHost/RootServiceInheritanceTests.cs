using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.DefaultShellHost;

/// <summary>
/// Tests for root service collection inheritance in shell service providers.
/// </summary>
public class RootServiceInheritanceTests : IDisposable
{
    private readonly List<CShells.DefaultShellHost> _hostsToDispose = [];

    public void Dispose()
    {
        foreach (var host in _hostsToDispose)
        {
            host.Dispose();
        }
    }

    #region Test Service Interfaces and Implementations

    /// <summary>
    /// A service registered only in the root service collection.
    /// </summary>
    public interface IRootOnlyService
    {
        string GetValue();
    }

    /// <summary>
    /// Implementation of <see cref="IRootOnlyService"/>.
    /// </summary>
    public class RootOnlyService : IRootOnlyService
    {
        public string GetValue() => "Root";
    }

    /// <summary>
    /// A service registered in both root and shell service collections.
    /// </summary>
    public interface ISharedService
    {
        string GetSource();
    }

    /// <summary>
    /// Root implementation of <see cref="ISharedService"/>.
    /// </summary>
    public class RootSharedService : ISharedService
    {
        public string GetSource() => "Root";
    }

    /// <summary>
    /// Shell-specific implementation of <see cref="ISharedService"/>.
    /// </summary>
    public class ShellSharedService : ISharedService
    {
        public string GetSource() => "Shell";
    }

    #endregion

    #region Test Feature

    /// <summary>
    /// A feature that overrides <see cref="ISharedService"/> with a shell-specific implementation.
    /// </summary>
    [ShellFeature("OverrideFeature")]
    public class OverrideFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ISharedService, ShellSharedService>();
        }
    }

    /// <summary>
    /// A feature that does not override any services.
    /// </summary>
    [ShellFeature("EmptyFeature")]
    public class EmptyFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // No service registrations
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a root service collection and provider with root services registered.
    /// </summary>
    private static (IServiceCollection Services, IServiceProvider Provider) CreateRootServicesWithSharedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRootOnlyService, RootOnlyService>();
        services.AddSingleton<ISharedService, RootSharedService>();
        return (services, services.BuildServiceProvider());
    }

    /// <summary>
    /// Creates a <see cref="DefaultShellHost"/> with root service inheritance enabled.
    /// </summary>
    private CShells.DefaultShellHost CreateHostWithRootServices(
        IServiceCollection rootServices,
        IServiceProvider rootProvider,
        params ShellSettings[] shellSettings)
    {
        var accessor = new RootServiceCollectionAccessor(rootServices);
        var host = new CShells.DefaultShellHost(
            shellSettings,
            [typeof(RootServiceInheritanceTests).Assembly],
            rootProvider,
            accessor);
        _hostsToDispose.Add(host);
        return host;
    }

    /// <summary>
    /// Internal implementation of <see cref="IRootServiceCollectionAccessor"/> for testing.
    /// </summary>
    private sealed class RootServiceCollectionAccessor : IRootServiceCollectionAccessor
    {
        public RootServiceCollectionAccessor(IServiceCollection services)
        {
            Services = services;
        }

        public IServiceCollection Services { get; }
    }

    #endregion

    #region Inheritance Tests

    [Fact(DisplayName = "Shell can resolve root-only service via inheritance")]
    public void Shell_CanResolveRootOnlyService_ViaInheritance()
    {
        // Arrange: Root registers IRootOnlyService and ISharedService. Shell has no overriding features.
        var (rootServices, rootProvider) = CreateRootServicesWithSharedService();
        var shellSettings = new ShellSettings(new ShellId("TestShell"), ["EmptyFeature"]);
        var host = CreateHostWithRootServices(rootServices, rootProvider, shellSettings);

        // Act: Build the shell and resolve IRootOnlyService from the shell provider.
        var shell = host.GetShell(new ShellId("TestShell"));
        var rootOnlyService = shell.ServiceProvider.GetService<IRootOnlyService>();

        // Assert: Service resolves successfully and matches the root implementation.
        Assert.NotNull(rootOnlyService);
        Assert.Equal("Root", rootOnlyService.GetValue());
    }

    [Fact(DisplayName = "Shell inherits shared service from root when not overridden")]
    public void Shell_InheritsSharedService_WhenNotOverridden()
    {
        // Arrange: Root registers ISharedService. Shell has no overriding features.
        var (rootServices, rootProvider) = CreateRootServicesWithSharedService();
        var shellSettings = new ShellSettings(new ShellId("TestShell"), ["EmptyFeature"]);
        var host = CreateHostWithRootServices(rootServices, rootProvider, shellSettings);

        // Act: Build the shell and resolve ISharedService from the shell provider.
        var shell = host.GetShell(new ShellId("TestShell"));
        var sharedService = shell.ServiceProvider.GetService<ISharedService>();

        // Assert: Service resolves to the root implementation.
        Assert.NotNull(sharedService);
        Assert.Equal("Root", sharedService.GetSource());
    }

    [Fact(DisplayName = "Shell with no features resolves root services")]
    public void Shell_WithNoFeatures_ResolvesRootServices()
    {
        // Arrange: Root registers services. Shell has no enabled features.
        var (rootServices, rootProvider) = CreateRootServicesWithSharedService();
        var shellSettings = new ShellSettings(new ShellId("TestShell"));
        var host = CreateHostWithRootServices(rootServices, rootProvider, shellSettings);

        // Act: Build the shell and resolve services from the shell provider.
        var shell = host.GetShell(new ShellId("TestShell"));
        var rootOnlyService = shell.ServiceProvider.GetService<IRootOnlyService>();
        var sharedService = shell.ServiceProvider.GetService<ISharedService>();

        // Assert: Both services resolve to root implementations.
        Assert.NotNull(rootOnlyService);
        Assert.Equal("Root", rootOnlyService.GetValue());
        Assert.NotNull(sharedService);
        Assert.Equal("Root", sharedService.GetSource());
    }

    #endregion

    #region Override Tests

    [Fact(DisplayName = "Shell feature can override root service registration")]
    public void ShellFeature_CanOverride_RootServiceRegistration()
    {
        // Arrange: Root registers ISharedService with RootSharedService.
        // A shell feature registers ISharedService with ShellSharedService.
        var (rootServices, rootProvider) = CreateRootServicesWithSharedService();
        var shellSettings = new ShellSettings(new ShellId("TestShell"), ["OverrideFeature"]);
        var host = CreateHostWithRootServices(rootServices, rootProvider, shellSettings);

        // Act: Build the shell and resolve ISharedService from the shell provider.
        var shell = host.GetShell(new ShellId("TestShell"));
        var sharedService = shell.ServiceProvider.GetService<ISharedService>();

        // Assert: The resolved implementation is ShellSharedService, not RootSharedService.
        Assert.NotNull(sharedService);
        Assert.Equal("Shell", sharedService.GetSource());
        Assert.IsType<ShellSharedService>(sharedService);
    }

    [Fact(DisplayName = "Shell override does not affect root-only services")]
    public void ShellOverride_DoesNotAffect_RootOnlyServices()
    {
        // Arrange: Root registers IRootOnlyService and ISharedService.
        // Shell overrides ISharedService but IRootOnlyService should still be inherited.
        var (rootServices, rootProvider) = CreateRootServicesWithSharedService();
        var shellSettings = new ShellSettings(new ShellId("TestShell"), ["OverrideFeature"]);
        var host = CreateHostWithRootServices(rootServices, rootProvider, shellSettings);

        // Act: Build the shell and resolve both services.
        var shell = host.GetShell(new ShellId("TestShell"));
        var rootOnlyService = shell.ServiceProvider.GetService<IRootOnlyService>();
        var sharedService = shell.ServiceProvider.GetService<ISharedService>();

        // Assert: Root-only service is still inherited, but shared service is overridden.
        Assert.NotNull(rootOnlyService);
        Assert.Equal("Root", rootOnlyService.GetValue());
        Assert.NotNull(sharedService);
        Assert.Equal("Shell", sharedService.GetSource());
    }

    [Fact(DisplayName = "Different shells can have different service implementations")]
    public void DifferentShells_CanHaveDifferentServiceImplementations()
    {
        // Arrange: Root registers ISharedService.
        // Shell1 overrides it, Shell2 does not.
        var (rootServices, rootProvider) = CreateRootServicesWithSharedService();
        var shell1Settings = new ShellSettings(new ShellId("Shell1"), ["OverrideFeature"]);
        var shell2Settings = new ShellSettings(new ShellId("Shell2"), ["EmptyFeature"]);
        var host = CreateHostWithRootServices(rootServices, rootProvider, shell1Settings, shell2Settings);

        // Act: Build both shells and resolve ISharedService from each.
        var shell1 = host.GetShell(new ShellId("Shell1"));
        var shell2 = host.GetShell(new ShellId("Shell2"));
        var shell1SharedService = shell1.ServiceProvider.GetService<ISharedService>();
        var shell2SharedService = shell2.ServiceProvider.GetService<ISharedService>();

        // Assert: Shell1 gets the override, Shell2 gets the root implementation.
        Assert.NotNull(shell1SharedService);
        Assert.Equal("Shell", shell1SharedService.GetSource());
        Assert.NotNull(shell2SharedService);
        Assert.Equal("Root", shell2SharedService.GetSource());
    }

    #endregion

    #region No Temporary Provider Tests

    [Fact(DisplayName = "BuildServiceProvider is only called once per shell")]
    public void BuildServiceProvider_IsOnlyCalledOnce_PerShell()
    {
        // This test validates the design by ensuring that the same shell context
        // is returned when GetShell is called multiple times.
        // The implementation in DefaultShellHost uses GetOrAdd pattern which ensures
        // BuildServiceProvider is only called once per shell.

        // Arrange
        var (rootServices, rootProvider) = CreateRootServicesWithSharedService();
        var shellSettings = new ShellSettings(new ShellId("TestShell"), ["EmptyFeature"]);
        var host = CreateHostWithRootServices(rootServices, rootProvider, shellSettings);

        // Act: Get the same shell multiple times
        var shell1 = host.GetShell(new ShellId("TestShell"));
        var shell2 = host.GetShell(new ShellId("TestShell"));

        // Assert: Same ShellContext instance is returned
        Assert.Same(shell1, shell2);
        Assert.Same(shell1.ServiceProvider, shell2.ServiceProvider);
    }

    #endregion
}
