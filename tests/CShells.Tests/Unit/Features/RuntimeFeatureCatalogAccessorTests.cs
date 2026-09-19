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

    private static Assembly CreateDynamicFeatureAssembly(
        string assemblyName,
        string typeName,
        string featureName,
        string? displayName = null,
        string? description = null)
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
