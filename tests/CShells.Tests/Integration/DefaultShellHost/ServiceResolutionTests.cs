using System.Reflection;
using System.Reflection.Emit;
using CShells.Configuration;
using CShells.Features;
using CShells.Hosting;
using CShells.Tests.Integration.ShellHost;
using CShells.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.DefaultShellHost;

/// <summary>
/// Tests for <see cref="DefaultShellHost"/> service resolution and dependency injection.
/// </summary>
public class ServiceResolutionTests : IDisposable
{
    private readonly List<Hosting.DefaultShellHost> _hostsToDispose = [];

    public void Dispose()
    {
        foreach (var host in _hostsToDispose)
        {
            host.Dispose();
        }
    }

    private Hosting.DefaultShellHost CreateHost(ShellSettings[] settings, Assembly[] assemblies)
    {
        var cache = new ShellSettingsCache();
        cache.Load(settings);
        var (services, provider) = TestFixtures.CreateRootServices();
        var accessor = TestFixtures.CreateRootServicesAccessor(services);
        var factory = new CShells.Features.DefaultShellFeatureFactory(provider);
        var exclusionRegistry = new ShellServiceExclusionRegistry([]);
        var host = new Hosting.DefaultShellHost(cache, assemblies, provider, accessor, factory, exclusionRegistry);
        _hostsToDispose.Add(host);
        return host;
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
        var host = CreateHost(settings, [assembly]);

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
        var host = CreateHost(settings, [assembly]);

        // Act
        var shell = host.GetShell(new("TestShell"));

        // Assert - Both services should be registered (parent dependency is auto-resolved)
        var parentService = shell.ServiceProvider.GetService<IParentService>();
        var childService = shell.ServiceProvider.GetService<IChildService>();
        Assert.NotNull(parentService);
        Assert.NotNull(childService);
    }

    [Fact(DisplayName = "GetShell feature constructor can accept ShellSettings")]
    public void GetShell_FeatureConstructorCanAcceptShellSettings()
    {
        // Arrange - Create a feature whose constructor accepts ShellSettings
        var assembly = CreateTestAssemblyWithShellSettingsConstructor();
        var settings = new[]
        {
            new ShellSettings(new("TestShell"), ["SettingsAwareFeature"])
        };
        var host = CreateHost(settings, [assembly]);

        // Act - This should not throw because ShellSettings is provided explicitly
        var shell = host.GetShell(new("TestShell"));

        // Assert - The service registered by the feature should be available
        var settingsService = shell.ServiceProvider.GetService<ISettingsAwareService>();
        Assert.NotNull(settingsService);
    }

    [Fact(DisplayName = "GetShell feature constructor with shell dependency throws")]
    public void GetShell_FeatureConstructorWithShellDependency_Throws()
    {
        // Arrange - Create a feature whose constructor depends on a shell-level service
        // According to the new design, this is NOT allowed
        var assembly = CreateTestAssemblyWithConstructorDependency();
        var settings = new[]
        {
            new ShellSettings(new("TestShell"), ["DependentFeature"])
        };
        var host = CreateHost(settings, [assembly]);

        // Act & Assert - This should throw because IBaseService is not available from root provider
        var ex = Assert.Throws<InvalidOperationException>(() => host.GetShell(new("TestShell")));
        Assert.Contains("DependentFeature", ex.Message);
        
        // Verify the inner exception is from ActivatorUtilities indicating constructor dependency resolution failure
        // The inner exception message indicates the service could not be resolved
        Assert.NotNull(ex.InnerException);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("Unable to resolve service", ex.InnerException.Message);
    }

    [Fact(DisplayName = "GetShell returns ShellSettings from service provider")]
    public void GetShell_ShellSettingsIsRegistered_ReturnsFromServiceProvider()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new("TestShell"))
        };
        var host = CreateHost(settings, []);

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
        var host = CreateHost(settings, []);

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

    public interface ISettingsAwareService
    {
        ShellId ShellId { get; }
    }
    public class SettingsAwareService(ShellId shellId) : ISettingsAwareService
    {
        public ShellId ShellId { get; } = shellId;
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
        // According to new design, this should FAIL because shell services cannot be injected into feature constructors
        CreateFeatureTypeWithConstructorDependency(moduleBuilder, "DependentFeature", ["BaseFeature"], typeof(IBaseService), typeof(IValidationService), typeof(ValidationService));

        return assemblyBuilder;
    }

    private static Assembly CreateTestAssemblyWithShellSettingsConstructor()
    {
        var assemblyName = new AssemblyName($"TestAssembly_{Guid.NewGuid():N}");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        // Create a feature whose constructor accepts ShellSettings (which is allowed)
        CreateFeatureTypeWithShellSettingsConstructor(moduleBuilder, "SettingsAwareFeature", [], typeof(ISettingsAwareService), typeof(SettingsAwareService));

        return assemblyBuilder;
    }

    private static void CreateFeatureTypeWithShellSettingsConstructor(ModuleBuilder moduleBuilder, string featureName, string[] dependencies, Type serviceInterface, Type serviceImplementation)
    {
        var typeName = $"{featureName}Startup";
        var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
        typeBuilder.AddInterfaceImplementation(typeof(IShellFeature));

        // Add a field to store ShellSettings
        var settingsField = typeBuilder.DefineField("_settings", typeof(ShellSettings), FieldAttributes.Private | FieldAttributes.InitOnly);

        // Define constructor that takes ShellSettings
        var constructor = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            [typeof(ShellSettings)]);

        var ctorIl = constructor.GetILGenerator();
        ctorIl.Emit(OpCodes.Ldarg_0);
        ctorIl.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        ctorIl.Emit(OpCodes.Ldarg_0);
        ctorIl.Emit(OpCodes.Ldarg_1);
        ctorIl.Emit(OpCodes.Stfld, settingsField);
        ctorIl.Emit(OpCodes.Ret);

        // Implement ConfigureServices method that registers the service
        // ConfigureServices(IServiceCollection services)
        var configureServicesMethod = typeBuilder.DefineMethod(
            "ConfigureServices",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            [typeof(IServiceCollection)]);

        var il = configureServicesMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_1); // services

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

    private static void CreateFeatureTypeWithConstructorDependency(ModuleBuilder moduleBuilder, string featureName, string[] dependencies, Type constructorDependency, Type serviceInterface, Type serviceImplementation)
    {
        var typeName = $"{featureName}Startup";
        var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
        typeBuilder.AddInterfaceImplementation(typeof(IShellFeature));

        // Add a field to store the constructor dependency
        var dependencyField = typeBuilder.DefineField("_dependency", constructorDependency, FieldAttributes.Private | FieldAttributes.InitOnly);

        // Define constructor that takes the dependency (shell-level service - NOT allowed in new design)
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
        // ConfigureServices(IServiceCollection services)
        var configureServicesMethod = typeBuilder.DefineMethod(
            "ConfigureServices",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            [typeof(IServiceCollection)]);

        var il = configureServicesMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_1); // services

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
