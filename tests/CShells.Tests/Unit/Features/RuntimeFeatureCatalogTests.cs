using System.Reflection;
using System.Reflection.Emit;
using CShells.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Tests.Unit.Features;

public class RuntimeFeatureCatalogTests
{
    [Fact(DisplayName = "RefreshAsync re-evaluates providers on each reconciliation pass")]
    public async Task RefreshAsync_ReevaluatesAssemblySourcesEveryTime()
    {
        // Arrange
        var refreshCalls = 0;
        var uniqueAssembly = CreateDynamicFeatureAssembly("RuntimeFeatureCatalogUnique", "UniqueFeature", "Unique");
        var catalog = new RuntimeFeatureCatalog(
            _ =>
            {
                refreshCalls++;
                return Task.FromResult<IReadOnlyCollection<Assembly>>([uniqueAssembly]);
            },
            NullLogger<RuntimeFeatureCatalog>.Instance);

        // Act
        var first = await catalog.RefreshAsync();
        var second = await catalog.RefreshAsync();

        // Assert
        Assert.Equal(2, refreshCalls);
        Assert.NotEqual(first.Generation, second.Generation);
        Assert.True(second.Generation > first.Generation);
        Assert.Contains(second.FeatureDescriptors, descriptor => descriptor.Id == "Unique");
    }

    [Fact(DisplayName = "RefreshAsync preserves the last committed catalog when duplicate feature IDs are discovered")]
    public async Task RefreshAsync_DuplicateFeatureIds_DoesNotReplaceCommittedSnapshot()
    {
        // Arrange
        var uniqueAssembly = CreateDynamicFeatureAssembly("RuntimeFeatureCatalogUniqueCommitted", "CommittedUniqueFeature", "Unique");
        var duplicateAssemblyOne = CreateDynamicFeatureAssembly("RuntimeFeatureCatalogDuplicateOne", "DuplicateFeatureOne", "Duplicate");
        var duplicateAssemblyTwo = CreateDynamicFeatureAssembly("RuntimeFeatureCatalogDuplicateTwo", "DuplicateFeatureTwo", "Duplicate");
        var useDuplicateAssemblies = false;
        var catalog = new RuntimeFeatureCatalog(
            _ => Task.FromResult<IReadOnlyCollection<Assembly>>(
                useDuplicateAssemblies
                    ? [duplicateAssemblyOne, duplicateAssemblyTwo]
                    : [uniqueAssembly]),
            NullLogger<RuntimeFeatureCatalog>.Instance);

        var committed = await catalog.RefreshAsync();
        useDuplicateAssemblies = true;

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.RefreshAsync());

        // Assert
        Assert.Contains("Duplicate feature name 'Duplicate'", exception.Message);
        Assert.Same(committed, catalog.CurrentSnapshot);
        Assert.Contains(catalog.CurrentSnapshot.FeatureDescriptors, descriptor => descriptor.Id == "Unique");
    }

    [Fact(DisplayName = "EnsureInitializedAsync initializes on first access and returns the committed snapshot")]
    public async Task EnsureInitializedAsync_InitializesAndReturnsCommittedSnapshot()
    {
        // Arrange
        var refreshCalls = 0;
        var uniqueAssembly = CreateDynamicFeatureAssembly("RuntimeFeatureCatalogGetSnapshot", "GetSnapshotFeature", "GetSnapshot");
        var catalog = new RuntimeFeatureCatalog(
            _ =>
            {
                refreshCalls++;
                return Task.FromResult<IReadOnlyCollection<Assembly>>([uniqueAssembly]);
            },
            NullLogger<RuntimeFeatureCatalog>.Instance);

        // Act
        await catalog.EnsureInitializedAsync();
        var first = catalog.CurrentSnapshot;
        await catalog.EnsureInitializedAsync();
        var second = catalog.CurrentSnapshot;

        // Assert: first access discovers once; subsequent access reuses the committed snapshot.
        Assert.Equal(1, refreshCalls);
        Assert.Same(first, second);
        Assert.Contains(first.FeatureDescriptors, descriptor => descriptor.Id == "GetSnapshot");
    }

    private static Assembly CreateDynamicFeatureAssembly(string assemblyName, string typeName, string featureName)
    {
        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new(assemblyName), AssemblyBuilderAccess.Run);
        var module = dynamicAssembly.DefineDynamicModule(assemblyName);
        var type = module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
        type.AddInterfaceImplementation(typeof(IShellFeature));

        var attributeConstructor = typeof(ShellFeatureAttribute).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException("ShellFeatureAttribute(string) constructor was not found.");
        type.SetCustomAttribute(new(attributeConstructor, [featureName]));
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
