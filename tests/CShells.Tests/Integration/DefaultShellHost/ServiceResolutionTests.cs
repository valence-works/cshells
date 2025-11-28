using System.Reflection;
using System.Reflection.Emit;
using CShells.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.DefaultShellHost;

/// <summary>
/// Tests for <see cref="CShells.DefaultShellHost"/> service resolution and dependency injection.
/// </summary>
public class ServiceResolutionTests : IDisposable
{
    private readonly List<CShells.DefaultShellHost> _hostsToDispose = [];

    public void Dispose()
    {
        foreach (var host in _hostsToDispose)
        {
            host.Dispose();
        }
    }

    [Fact(DisplayName = "GetShell with enabled features resolves and configures services")]
    public void GetShell_WithEnabledFeatures_ResolvesAndConfiguresServices()
    {
        // Arrange
        var assembly = CreateTestAssemblyWithService(typeof(ITestService), typeof(TestService), "TestFeature");
        var settings = new[]
        {
            new ShellSettings(new("TestShell"), ["TestFeature"])
        };
        var host = new CShells.DefaultShellHost(settings, [assembly]);
        _hostsToDispose.Add(host);

        // Act
        var shell = host.GetShell(new("TestShell"));

        // Assert
        Assert.NotNull(shell);
        var testService = shell.ServiceProvider.GetService<ITestService>();
        Assert.NotNull(testService);
    }

    [Fact(DisplayName = "GetShell with feature dependencies configures in correct order")]
    public void GetShell_WithFeatureDependencies_ConfiguresInCorrectOrder()
    {
        // Arrange
        var assembly = CreateTestAssemblyWithDependencies();
        var settings = new[]
        {
            new ShellSettings(new("TestShell"), ["ChildFeature"])
        };
        var host = new CShells.DefaultShellHost(settings, [assembly]);
        _hostsToDispose.Add(host);

        // Act
        var shell = host.GetShell(new("TestShell"));

        // Assert - Both services should be registered (parent dependency is auto-resolved)
        var parentService = shell.ServiceProvider.GetService<IParentService>();
        var childService = shell.ServiceProvider.GetService<IChildService>();
        Assert.NotNull(parentService);
        Assert.NotNull(childService);
    }

    [Fact(DisplayName = "GetShell feature constructor can resolve dependency services")]
    public void GetShell_FeatureConstructorCanResolveDependencyServices()
    {
        // Arrange - Create features where child feature constructor depends on parent's registered service
        var assembly = CreateTestAssemblyWithConstructorDependency();
        var settings = new[]
        {
            new ShellSettings(new("TestShell"), ["DependentFeature"])
        };
        var host = new CShells.DefaultShellHost(settings, [assembly]);
        _hostsToDispose.Add(host);

        // Act - This should not throw
        var shell = host.GetShell(new("TestShell"));

        // Assert - Both services should be registered and the validation service should exist
        var baseService = shell.ServiceProvider.GetService<IBaseService>();
        var validationService = shell.ServiceProvider.GetService<IValidationService>();
        Assert.NotNull(baseService);
        Assert.NotNull(validationService);
    }

    [Fact(DisplayName = "GetShell returns ShellSettings from service provider")]
    public void GetShell_ShellSettingsIsRegistered_ReturnsFromServiceProvider()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new("TestShell"))
        };
        var host = new CShells.DefaultShellHost(settings, []);
        _hostsToDispose.Add(host);

        // Act
        var shell = host.GetShell(new("TestShell"));
        var resolvedSettings = shell.ServiceProvider.GetService<ShellSettings>();

        // Assert
        Assert.NotNull(resolvedSettings);
        Assert.Equal("TestShell", resolvedSettings.Id.Name);
    }

    [Fact(DisplayName = "GetShell returns ShellContext from service provider")]
    public void GetShell_ShellContextIsRegistered_ReturnsFromServiceProvider()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new("TestShell"))
        };
        var host = new CShells.DefaultShellHost(settings, []);
        _hostsToDispose.Add(host);

        // Act
        var shell = host.GetShell(new("TestShell"));
        var resolvedContext = shell.ServiceProvider.GetRequiredService<ShellContext>();

        // Assert
        Assert.NotNull(resolvedContext);
        Assert.Same(shell, resolvedContext);
        Assert.Equal("TestShell", resolvedContext.Id.Name);
    }

    // Helper interfaces and test implementations
    public interface ITestService { }
    public class TestService : ITestService { }

    public interface IParentService { }
    public class ParentService : IParentService { }

    public interface IChildService { }
    public class ChildService : IChildService { }

    public interface IBaseService { }
    public class BaseService : IBaseService { }

    public interface IValidationService { }
    public class ValidationService : IValidationService
    {
        public ValidationService(IBaseService baseService)
        {
            // Validation service depends on base service in constructor
            ArgumentNullException.ThrowIfNull(baseService);
        }
    }

    private static Assembly CreateTestAssemblyWithService(Type serviceInterface, Type serviceImplementation, string featureName)
    {
        var assemblyName = new AssemblyName($"TestAssembly_{Guid.NewGuid():N}");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        TestAssemblyBuilder.CreateFeatureType(moduleBuilder, featureName, [], serviceInterface, serviceImplementation);

        return assemblyBuilder;
    }

    private static Assembly CreateTestAssemblyWithDependencies()
    {
        var assemblyName = new AssemblyName($"TestAssembly_{Guid.NewGuid():N}");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        // Create ParentFeature startup
        TestAssemblyBuilder.CreateFeatureType(moduleBuilder, "ParentFeature", [], typeof(IParentService), typeof(ParentService));

        // Create ChildFeature startup with dependency on ParentFeature
        TestAssemblyBuilder.CreateFeatureType(moduleBuilder, "ChildFeature", ["ParentFeature"], typeof(IChildService), typeof(ChildService));

        return assemblyBuilder;
    }

    private static Assembly CreateTestAssemblyWithConstructorDependency()
    {
        var assemblyName = new AssemblyName($"TestAssembly_{Guid.NewGuid():N}");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        // BaseFeature registers IBaseService
        TestAssemblyBuilder.CreateFeatureType(moduleBuilder, "BaseFeature", [], typeof(IBaseService), typeof(BaseService));

        // DependentFeature depends on BaseFeature and its constructor requires IBaseService
        // Then it registers IValidationService
        CreateFeatureTypeWithConstructorDependency(moduleBuilder, "DependentFeature", ["BaseFeature"], typeof(IBaseService), typeof(IValidationService), typeof(ValidationService));

        return assemblyBuilder;
    }

    private static void CreateFeatureTypeWithConstructorDependency(ModuleBuilder moduleBuilder, string featureName, string[] dependencies, Type constructorDependency, Type serviceInterface, Type serviceImplementation)
    {
        var typeName = $"{featureName}Startup";
        var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
        typeBuilder.AddInterfaceImplementation(typeof(IShellFeature));

        // Add a field to store the constructor dependency
        var dependencyField = typeBuilder.DefineField("_dependency", constructorDependency, FieldAttributes.Private | FieldAttributes.InitOnly);

        // Define constructor that takes the dependency
        var constructor = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            [constructorDependency]);

        var ctorIl = constructor.GetILGenerator();
        ctorIl.Emit(OpCodes.Ldarg_0);
        ctorIl.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        ctorIl.Emit(OpCodes.Ldarg_0);
        ctorIl.Emit(OpCodes.Ldarg_1);
        ctorIl.Emit(OpCodes.Stfld, dependencyField);
        ctorIl.Emit(OpCodes.Ret);

        // Implement ConfigureServices method that registers the service
        var configureServicesMethod = typeBuilder.DefineMethod(
            "ConfigureServices",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            [typeof(IServiceCollection)]);

        var il = configureServicesMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_1);

        var addSingletonMethod = typeof(ServiceCollectionServiceExtensions)
            .GetMethods()
            .First(m => m.Name == "AddSingleton" &&
                        m.IsGenericMethod &&
                        m.GetGenericArguments().Length == 2 &&
                        m.GetParameters().Length == 1)
            .MakeGenericMethod(serviceInterface, serviceImplementation);

        il.Emit(OpCodes.Call, addSingletonMethod);
        il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ret);

        // Add the ShellFeatureAttribute
        var attributeConstructor = typeof(ShellFeatureAttribute).GetConstructor([typeof(string)])!;
        var attributeBuilder = new CustomAttributeBuilder(
            attributeConstructor,
            [featureName],
            [typeof(ShellFeatureAttribute).GetProperty("DependsOn")!],
            [dependencies]);
        typeBuilder.SetCustomAttribute(attributeBuilder);

        typeBuilder.CreateType();
    }
}
