using CShells.Configuration;
using CShells.DependencyInjection;
using CShells.Features;
using CShells.Hosting;
using CShells.Tests.Integration.ShellHost;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.TestHelpers;

/// <summary>
/// Shared helper for creating and reusing <see cref="DefaultShellHost"/> instances.
/// </summary>
public sealed class DefaultShellHostFixture : IDisposable
{
    private readonly List<Hosting.DefaultShellHost> _hosts = [];
    private readonly IServiceProvider _rootProvider;
    private readonly IServiceCollection _rootServices;
    private readonly IRootServiceCollectionAccessor _accessor;
    private readonly IShellFeatureFactory _featureFactory;

    public IServiceProvider RootProvider => _rootProvider;
    public IRootServiceCollectionAccessor RootAccessor => _accessor;
    public IShellFeatureFactory FeatureFactory => _featureFactory;

    public DefaultShellHostFixture()
    {
        (_rootServices, _rootProvider) = TestFixtures.CreateRootServices();
        _accessor = TestFixtures.CreateRootServicesAccessor(_rootServices);
        _featureFactory = new DefaultShellFeatureFactory(_rootProvider);
    }

    public Hosting.DefaultShellHost CreateHost(IEnumerable<ShellSettings> shells, params System.Reflection.Assembly[] assemblies)
    {
        var cache = BuildCache(shells);
        return CreateHost(cache, assemblies);
    }

    public Hosting.DefaultShellHost CreateHost(ShellSettingsCache cache, params System.Reflection.Assembly[] assemblies)
    {
        var exclusionRegistry = new ShellServiceExclusionRegistry([]);
        var host = new Hosting.DefaultShellHost(cache, assemblies, _rootProvider, _accessor, _featureFactory, exclusionRegistry);
        _hosts.Add(host);
        return host;
    }

    public Hosting.DefaultShellHost CreateWeatherHost() =>
        CreateHost([new(new("Default"), ["Weather"])], typeof(TestFixtures).Assembly);

    private static ShellSettingsCache BuildCache(IEnumerable<ShellSettings> shells)
    {
        var cache = new ShellSettingsCache();
        cache.Load(shells.ToList());
        return cache;
    }

    public ShellSettingsCache CreateCache(IEnumerable<ShellSettings> shells)
    {
        return BuildCache(shells);
    }

    public void Dispose()
    {
        foreach (var host in _hosts)
        {
            host.Dispose();
        }
        _hosts.Clear();
    }
}
