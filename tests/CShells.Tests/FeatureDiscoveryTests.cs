using System.Reflection;
using System.Reflection.Emit;
using CShells.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CShells.Tests;

public class FeatureDiscoveryTests
{
    [Fact]
    public void DiscoverFeatures_WithNullAssemblies_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => FeatureDiscovery.DiscoverFeatures(null!).ToList());
        Assert.Equal("assemblies", ex.ParamName);
    }

    [Fact]
    public void DiscoverFeatures_WithEmptyAssemblies_ReturnsEmptyCollection()
    {
        // Act
        var features = FeatureDiscovery.DiscoverFeatures([]);

        // Assert
        Assert.Empty(features);
    }

    [Fact]
    public void DiscoverFeatures_WithNullAssemblyInCollection_SkipsNullAssembly()
    {
        // Arrange
        var assemblies = new Assembly?[] { null };

        // Act
        var features = FeatureDiscovery.DiscoverFeatures(assemblies!);

        // Assert
        Assert.Empty(features);
    }

    [Fact]
    public void DiscoverFeatures_WithValidFeature_ReturnsFeatureDescriptor()
    {
        // Arrange - use assembly with only valid features
        var assembly = CreateTestAssembly(
            ("ValidTestFeature", typeof(IShellStartup), Array.Empty<string>(), Array.Empty<object>())
        );

        // Act
        var features = FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList();

        // Assert
        var feature = features.FirstOrDefault(f => f.Id == "ValidTestFeature");
        Assert.NotNull(feature);
        Assert.Equal("ValidTestFeature", feature.Id);
        Assert.NotNull(feature.StartupType);
    }

    [Fact]
    public void DiscoverFeatures_WithFeatureHavingDependencies_SetsDependencies()
    {
        // Arrange - use assembly with feature that has dependencies
        var assembly = CreateTestAssembly(
            ("FeatureWithDeps", typeof(IShellStartup), new[] { "Dependency1", "Dependency2" }, Array.Empty<object>())
        );

        // Act
        var features = FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList();

        // Assert
        var feature = features.FirstOrDefault(f => f.Id == "FeatureWithDeps");
        Assert.NotNull(feature);
        Assert.Equal(new[] { "Dependency1", "Dependency2" }, feature.Dependencies);
    }

    [Fact]
    public void DiscoverFeatures_WithFeatureHavingMetadata_SetsMetadata()
    {
        // Arrange - use assembly with feature that has metadata
        var assembly = CreateTestAssembly(
            ("FeatureWithMeta", typeof(IShellStartup), Array.Empty<string>(), new object[] { "key1", "value1", "key2", "value2" })
        );

        // Act
        var features = FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList();

        // Assert
        var feature = features.FirstOrDefault(f => f.Id == "FeatureWithMeta");
        Assert.NotNull(feature);
        Assert.Equal("value1", feature.Metadata["key1"]);
        Assert.Equal("value2", feature.Metadata["key2"]);
    }

    [Fact]
    public void DiscoverFeatures_WithTypeMissingIShellStartup_ThrowsInvalidOperationException()
    {
        // Arrange - create assembly with a type that has ShellFeature but doesn't implement IShellStartup
        var assembly = CreateTestAssembly(
            ("InvalidFeature", null, Array.Empty<string>(), Array.Empty<object>())
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList());
        Assert.Contains("does not implement IShellStartup", ex.Message);
    }

    [Fact]
    public void DiscoverFeatures_WithDuplicateFeatureNames_ThrowsInvalidOperationException()
    {
        // Arrange - create assembly with two features having the same name
        var assembly = CreateTestAssembly(
            ("DuplicateFeatureName", typeof(IShellStartup), Array.Empty<string>(), Array.Empty<object>()),
            ("DuplicateFeatureName", typeof(IShellStartup), Array.Empty<string>(), Array.Empty<object>())
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList());
        Assert.Contains("Duplicate feature name", ex.Message);
        Assert.Contains("DuplicateFeatureName", ex.Message);
    }

    [Fact]
    public void DiscoverFeatures_WithMultipleValidFeatures_ReturnsAllFeatures()
    {
        // Arrange
        var assembly = CreateTestAssembly(
            ("Feature1", typeof(IShellStartup), Array.Empty<string>(), Array.Empty<object>()),
            ("Feature2", typeof(IShellStartup), new[] { "Feature1" }, Array.Empty<object>())
        );

        // Act
        var features = FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList();

        // Assert
        Assert.Equal(2, features.Count);
        Assert.Contains(features, f => f.Id == "Feature1");
        Assert.Contains(features, f => f.Id == "Feature2");
    }

    [Fact]
    public void DiscoverFeatures_WithOddMetadataElements_ThrowsInvalidOperationException()
    {
        // Arrange - create assembly with odd number of metadata elements
        var assembly = CreateTestAssembly(
            ("FeatureWithOddMetadata", typeof(IShellStartup), Array.Empty<string>(), new object[] { "key1", "value1", "orphanKey" })
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => FeatureDiscovery.DiscoverFeatures(new[] { assembly }).ToList());
        Assert.Contains("odd number of metadata elements", ex.Message);
        Assert.Contains("FeatureWithOddMetadata", ex.Message);
    }

    /// <summary>
    /// Creates a dynamic assembly with test feature types for testing purposes.
    /// </summary>
    private static Assembly CreateTestAssembly(params (string FeatureName, Type? ImplementInterface, string[] Dependencies, object[] Metadata)[] featureDefinitions)
    {
        var assemblyName = new AssemblyName($"TestAssembly_{Guid.NewGuid():N}");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var uniqueCounter = 0;
        foreach (var (featureName, implementInterface, dependencies, metadata) in featureDefinitions)
        {
            var typeName = $"{featureName}_{uniqueCounter++}";
            
            // Create the type, optionally implementing IShellStartup
            TypeBuilder typeBuilder;
            if (implementInterface == typeof(IShellStartup))
            {
                typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
                typeBuilder.AddInterfaceImplementation(typeof(IShellStartup));
                
                // Implement ConfigureServices method
                var configureServicesMethod = typeBuilder.DefineMethod(
                    "ConfigureServices",
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(void),
                    [typeof(IServiceCollection)]);
                var ilConfigureServices = configureServicesMethod.GetILGenerator();
                ilConfigureServices.Emit(OpCodes.Ret);
                
                // Implement Configure method
                var configureMethod = typeBuilder.DefineMethod(
                    "Configure",
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(void),
                    [typeof(IApplicationBuilder), typeof(IHostEnvironment)]);
                var ilConfigure = configureMethod.GetILGenerator();
                ilConfigure.Emit(OpCodes.Ret);
            }
            else
            {
                typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
            }
            
            // Add the ShellFeatureAttribute
            var attributeConstructor = typeof(ShellFeatureAttribute).GetConstructor([typeof(string)])!;
            var attributeBuilder = new CustomAttributeBuilder(
                attributeConstructor,
                [featureName],
                [
                    typeof(ShellFeatureAttribute).GetProperty("DependsOn")!,
                    typeof(ShellFeatureAttribute).GetProperty("Metadata")!
                ],
                [dependencies, metadata]);
            typeBuilder.SetCustomAttribute(attributeBuilder);
            
            typeBuilder.CreateType();
        }

        return assemblyBuilder;
    }
}
