using System.Reflection;
using CShells.Nuplane;
using Microsoft.Extensions.DependencyInjection;
using Nuplane.Loading;

namespace CShells.Tests.Integration.Nuplane;

public sealed class NuplaneFeatureAssemblyProviderTests
{
    [Fact]
    public async Task ProviderFlattensLoadedAssembliesInPackageOrderAndIgnoresReferences()
    {
        var first = typeof(NuplaneFeatureAssemblyProviderTests).Assembly;
        var second = typeof(string).Assembly;
        var catalog = new FakePackageAssemblyCatalog
        {
            Packages =
            [
                FakePackageAssemblyCatalog.CreatePackage("first", first),
                FakePackageAssemblyCatalog.CreatePackage("second", second, first)
            ]
        };
        var provider = new NuplaneFeatureAssemblyProvider(catalog);
        using var services = new ServiceCollection().BuildServiceProvider();

        var assemblies = (await provider.GetAssembliesAsync(services)).ToArray();

        Assert.Equal([first, second, first], assemblies);
        Assert.DoesNotContain(assemblies, assembly => assembly.GetName().Name == "not-loaded");
        Assert.Equal(1, catalog.QueryCount);
    }

    [Fact]
    public async Task ProviderReturnsEmptyForUnavailableCatalogAndPropagatesCancellation()
    {
        var catalog = new FakePackageAssemblyCatalog();
        var provider = new NuplaneFeatureAssemblyProvider(catalog);
        using var services = new ServiceCollection().BuildServiceProvider();

        Assert.Empty(await provider.GetAssembliesAsync(services));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetAssembliesAsync(services, cancellation.Token));
    }

    [Fact]
    public async Task ProviderHonorsCancellationWhenCatalogIgnoresItsToken()
    {
        var catalog = new FakePackageAssemblyCatalog
        {
            Query = _ => Task.FromResult<IReadOnlyList<PackageAssemblies>>(
                [FakePackageAssemblyCatalog.CreatePackage("features", typeof(NuplaneFeatureAssemblyProviderTests).Assembly)])
        };
        var provider = new NuplaneFeatureAssemblyProvider(catalog);
        using var services = new ServiceCollection().BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();

        catalog.Query = async _ =>
        {
            cancellation.Cancel();
            await Task.Yield();
            return [FakePackageAssemblyCatalog.CreatePackage("features", typeof(NuplaneFeatureAssemblyProviderTests).Assembly)];
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetAssembliesAsync(services, cancellation.Token));
    }
}
