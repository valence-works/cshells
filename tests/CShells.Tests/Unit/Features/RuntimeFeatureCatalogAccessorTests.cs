using System.Reflection;
using System.Reflection.Emit;
using CShells.DependencyInjection;
using CShells.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Tests.Unit.Features;

public class RuntimeFeatureCatalogAccessorTests
{
    [Fact(DisplayName = "RefreshAsync exposes a typed snapshot with descriptor count and fields")]
    public async Task RefreshAsync_ExposesTypedDescriptors()
    {
        // Arrange
        var assembly = CreateDynamicFeatureAssembly(
            "RuntimeFeatureCatalogAccessorTyped",
            "TypedFeature",
            "Typed",
            displayName: "Typed Feature",
            description: "A typed feature.");
        IRuntimeFeatureCatalog accessor = new RuntimeFeatureCatalogAccessor(
            new RuntimeFeatureCatalog(
                _ => Task.FromResult<IReadOnlyCollection<Assembly>>([assembly]),
                NullLogger<RuntimeFeatureCatalog>.Instance));

        // Act
        var snapshot = await accessor.RefreshAsync();

        // Assert
        var descriptorCount = snapshot.FeatureDescriptors.Count;
        Assert.Equal(1, descriptorCount);
        var descriptor = snapshot.FeatureDescriptors.Single();
        Assert.Equal("Typed", descriptor.Id);
        Assert.Equal("Typed", descriptor.Name);
        Assert.Equal("Typed Feature", descriptor.DisplayName);
        Assert.Equal("A typed feature.", descriptor.Description);
    }

    [Fact(DisplayName = "DisplayName falls back to Id and Description is null when not declared")]
    public async Task RefreshAsync_DefaultsDisplayNameAndDescription()
    {
        // Arrange
        var assembly = CreateDynamicFeatureAssembly("RuntimeFeatureCatalogAccessorBare", "BareFeature", "Bare");
        IRuntimeFeatureCatalog accessor = new RuntimeFeatureCatalogAccessor(
            new RuntimeFeatureCatalog(
                _ => Task.FromResult<IReadOnlyCollection<Assembly>>([assembly]),
                NullLogger<RuntimeFeatureCatalog>.Instance));

        // Act
        var snapshot = await accessor.RefreshAsync();

        // Assert
        var descriptor = snapshot.FeatureDescriptors.Single();
        Assert.Equal("Bare", descriptor.DisplayName);
        Assert.Null(descriptor.Description);
    }

    [Fact(DisplayName = "CurrentSnapshot reflects the latest committed refresh")]
    public async Task CurrentSnapshot_TracksGenerations()
    {
        // Arrange
        var assembly = CreateDynamicFeatureAssembly("RuntimeFeatureCatalogAccessorGen", "GenFeature", "Gen");
        IRuntimeFeatureCatalog accessor = new RuntimeFeatureCatalogAccessor(
            new RuntimeFeatureCatalog(
                _ => Task.FromResult<IReadOnlyCollection<Assembly>>([assembly]),
                NullLogger<RuntimeFeatureCatalog>.Instance));

        // Act
        var first = await accessor.RefreshAsync();
        var second = await accessor.RefreshAsync();

        // Assert
        Assert.True(second.Generation > first.Generation);
        Assert.Equal(second.Generation, accessor.CurrentSnapshot.Generation);
    }

    [Fact(DisplayName = "AddCShells registers IRuntimeFeatureCatalog for direct resolution")]
    public async Task AddCShells_RegistersPublicContract()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCShells(cshells => cshells.WithAssemblyContaining<RuntimeFeatureCatalogAccessorTests>());

        await using var sp = services.BuildServiceProvider();

        // Act
        var catalog = sp.GetService<IRuntimeFeatureCatalog>();

        // Assert
        Assert.NotNull(catalog);
        Assert.IsType<RuntimeFeatureCatalogAccessor>(catalog);
        var snapshot = await catalog.RefreshAsync();
        Assert.NotNull(snapshot);
    }

    [Fact]
    public async Task GetSnapshotAsync_PreservesDetailsWithoutRefreshingOrConstructingFeatures()
    {
        var assembly = CreateDynamicFeatureAssembly(
            "RuntimeFeatureCatalogDetailed", "DetailedFeature", "Detailed",
            displayName: "Detailed Feature", description: "Discovery metadata.", throwOnConstruction: true);
        var discoveryCount = 0;
        IRuntimeFeatureCatalog accessor = new RuntimeFeatureCatalogAccessor(new RuntimeFeatureCatalog(_ =>
        {
            discoveryCount++;
            return Task.FromResult<IReadOnlyCollection<Assembly>>([assembly]);
        }));

        var first = await accessor.GetSnapshotAsync();
        var second = await accessor.GetSnapshotAsync();

        Assert.Same(first, second);
        Assert.Equal(1, discoveryCount);
        Assert.Same(assembly, Assert.Single(first.Assemblies));
        var descriptor = Assert.Single(first.FeatureDescriptors);
        Assert.Equal(assembly.GetType("DetailedFeature"), descriptor.StartupType);
        Assert.Equal("Detailed Feature", descriptor.Metadata["DisplayName"]);
        Assert.Equal("Discovery metadata.", descriptor.Metadata["Description"]);
        Assert.Same(descriptor, first.FeatureMap["detailed"]);
        Assert.Equal(first.Generation, accessor.CurrentSnapshot.Generation);

        var refreshed = await accessor.RefreshAsync();
        var currentDetails = await accessor.GetSnapshotAsync();
        Assert.Equal(2, discoveryCount);
        Assert.True(refreshed.Generation > first.Generation);
        Assert.Equal(refreshed.Generation, currentDetails.Generation);
        Assert.Equal(refreshed.RefreshedAt, currentDetails.RefreshedAt);
    }

    [Fact]
    public async Task GetSnapshotAsync_PropagatesCancellationBeforeInitialization()
    {
        IRuntimeFeatureCatalog accessor = new RuntimeFeatureCatalogAccessor(new RuntimeFeatureCatalog(
            _ => throw new InvalidOperationException("Discovery must not start after cancellation.")));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => accessor.GetSnapshotAsync(cancellation.Token));
        Assert.Throws<InvalidOperationException>(() => accessor.CurrentSnapshot);
    }

    [Fact]
    public async Task GetSnapshotAsync_PropagatesDiscoveryFailureWithoutCommittingASnapshot()
    {
        var expected = new InvalidOperationException("Discovery failed.");
        IRuntimeFeatureCatalog accessor = new RuntimeFeatureCatalogAccessor(new RuntimeFeatureCatalog(_ => throw expected));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => accessor.GetSnapshotAsync());

        Assert.Same(expected, actual);
        Assert.Throws<InvalidOperationException>(() => accessor.CurrentSnapshot);
    }

    [Fact]
    public async Task ProjectionOnlyImplementations_KeepTypedReadsAndExplicitlyRefuseDetailedReads()
    {
        IRuntimeFeatureCatalog builtIn = new RuntimeFeatureCatalogAccessor(new RuntimeFeatureCatalog(
            _ => Task.FromResult<IReadOnlyCollection<Assembly>>([])));
        var snapshot = await builtIn.RefreshAsync();
        IRuntimeFeatureCatalog projectionOnly = new ProjectionOnlyCatalog(snapshot);

        await projectionOnly.EnsureInitializedAsync();

        Assert.Same(snapshot, projectionOnly.CurrentSnapshot);
        Assert.Same(snapshot, await projectionOnly.RefreshAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => projectionOnly.GetSnapshotAsync());
    }

    private sealed class ProjectionOnlyCatalog(IRuntimeFeatureCatalogSnapshot snapshot) : IRuntimeFeatureCatalog
    {
        public IRuntimeFeatureCatalogSnapshot CurrentSnapshot => snapshot;
        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IRuntimeFeatureCatalogSnapshot> RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }

    private static Assembly CreateDynamicFeatureAssembly(
        string assemblyName,
        string typeName,
        string featureName,
        string? displayName = null,
        string? description = null,
        bool throwOnConstruction = false)
    {
        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new(assemblyName), AssemblyBuilderAccess.Run);
        var module = dynamicAssembly.DefineDynamicModule(assemblyName);
        var type = module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
        type.AddInterfaceImplementation(typeof(IShellFeature));

        var attributeConstructor = typeof(ShellFeatureAttribute).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException("ShellFeatureAttribute(string) constructor was not found.");

        var namedProperties = new List<PropertyInfo>();
        var propertyValues = new List<object?>();
        if (displayName is not null)
        {
            namedProperties.Add(typeof(ShellFeatureAttribute).GetProperty(nameof(ShellFeatureAttribute.DisplayName))!);
            propertyValues.Add(displayName);
        }

        if (description is not null)
        {
            namedProperties.Add(typeof(ShellFeatureAttribute).GetProperty(nameof(ShellFeatureAttribute.Description))!);
            propertyValues.Add(description);
        }

        type.SetCustomAttribute(new(
            attributeConstructor,
            [featureName],
            [.. namedProperties],
            [.. propertyValues]));
        if (throwOnConstruction)
        {
            var constructor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            var body = constructor.GetILGenerator();
            body.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(Type.EmptyTypes)!);
            body.Emit(OpCodes.Throw);
        }
        else
            type.DefineDefaultConstructor(MethodAttributes.Public);

        var configureServices = type.DefineMethod(
            nameof(IShellFeature.ConfigureServices),
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            [typeof(IServiceCollection)]);
        configureServices.GetILGenerator().Emit(OpCodes.Ret);
        type.DefineMethodOverride(configureServices, typeof(IShellFeature).GetMethod(nameof(IShellFeature.ConfigureServices))!);

        _ = type.CreateType();
        return dynamicAssembly;
    }
}
