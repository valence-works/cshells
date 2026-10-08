using CShells.DependencyInjection;
using CShells.Lifecycle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nuplane.Abstractions;
using Nuplane.Loading;

namespace CShells.Tests.Integration.Nuplane;

internal sealed class NuplaneTestHost(ServiceProvider provider) : IServiceProvider, IAsyncDisposable
{
    private readonly ServiceProvider rootProvider = provider;
    private int _disposed;

    public object? GetService(Type serviceType) => rootProvider.GetService(serviceType);

    public T GetRequiredService<T>() where T : notnull => rootProvider.GetRequiredService<T>();

    public static NuplaneTestHost Build(
        Action<IServiceCollection> configureServices,
        Action<CShellsBuilder> configureShells)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        configureServices(services);
        services.AddCShells(configureShells);
        return new NuplaneTestHost(services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        }));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var failures = new List<Exception>();
        try
        {
            if (rootProvider.GetService<IShellRegistry>() is { } registry)
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var shell in registry.GetActiveShells())
                    names.Add(shell.Descriptor.Name);

                string? cursor = null;
                do
                {
                    try
                    {
                        var page = await registry.ListAsync(new ShellListQuery(cursor, ShellListQuery.MaxLimit))
                            .WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        foreach (var item in page.Items)
                            names.Add(item.Name);
                        cursor = page.NextCursor;
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception);
                        break;
                    }
                }
                while (cursor is not null);

                foreach (var name in names)
                {
                    foreach (var shell in registry.GetAll(name))
                    {
                        try
                        {
                            var drain = await registry.DrainAsync(shell).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                            await drain.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        }
                        catch (Exception exception)
                        {
                            failures.Add(exception);
                        }
                    }
                }
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            await rootProvider.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (failures.Count > 0)
            throw new AggregateException("Nuplane test host teardown failed.", failures);
    }
}

internal sealed class FakePackageAssemblyCatalog : IPackageAssemblyCatalog
{
    private IReadOnlyList<PackageAssemblies> _packages = [];

    public int QueryCount { get; private set; }

    public Func<CancellationToken, Task<IReadOnlyList<PackageAssemblies>>>? Query { get; set; }

    public IReadOnlyList<PackageAssemblies> Packages
    {
        get => _packages;
        set => _packages = value;
    }

    public async Task<IReadOnlyList<PackageAssemblies>> GetPackagedAssembliesAsync(CancellationToken cancellationToken = default)
    {
        QueryCount++;
        if (Query is { } query)
            return await query(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return _packages;
    }

    public async Task<PackageAssemblies?> GetPackagedAssembliesAsync(string packageId, CancellationToken cancellationToken = default)
    {
        var packages = await GetPackagedAssembliesAsync(cancellationToken).ConfigureAwait(false);
        return packages.FirstOrDefault(package => string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
    }

    public static PackageAssemblies CreatePackage(string id, params System.Reflection.Assembly[] assemblies) => new(
        id,
        "1.0.0",
        assemblies,
        [new PackageAssemblyReference("/package/not-loaded.dll", "not-loaded.dll", "net10.0", "PrimaryLoadAssembly", "test reference")],
        Enum.GetValues<PackageLoadMode>()[0],
        FrameworkIntegrationSafe: false);

    public static PackageChangeSet ChangeSet(
        IReadOnlyList<ResolvedPackage>? added = null,
        IReadOnlyList<ResolvedPackage>? updated = null,
        IReadOnlyList<string>? removed = null) => new(
            added ?? [],
            updated ?? [],
            removed ?? [],
            "test",
            DateTimeOffset.UtcNow);

    public static ResolvedPackage Resolved(string id) => new(
        id,
        "1.0.0",
        "test-feed",
        "/tmp/nuplane-test-package",
        DateTimeOffset.UtcNow,
        "test-source");
}
