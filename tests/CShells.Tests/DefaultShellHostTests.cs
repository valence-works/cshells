using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests;

public class DefaultShellHostTests : IDisposable
{
    private readonly List<DefaultShellHost> _hostsToDispose = [];

    public void Dispose()
    {
        foreach (var host in _hostsToDispose)
        {
            host.Dispose();
        }
    }

    [Fact]
    public void Constructor_WithNullShellSettings_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new DefaultShellHost(null!));
        Assert.Equal("shellSettings", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullAssemblies_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new[] { new ShellSettings(new ShellId("Test")) };
        IEnumerable<Assembly>? nullAssemblies = null;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new DefaultShellHost(settings, nullAssemblies!));
        Assert.Equal("assemblies", ex.ParamName);
    }

    [Fact]
    public void DefaultShell_WithNoShells_ThrowsInvalidOperationException()
    {
        // Arrange
        var host = new DefaultShellHost([], []);
        _hostsToDispose.Add(host);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _ = host.DefaultShell);
        Assert.Contains("No shells have been configured", ex.Message);
    }

    [Fact]
    public void DefaultShell_WithDefaultShellConfigured_ReturnsDefaultShell()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new ShellId("Default")),
            new ShellSettings(new ShellId("Other"))
        };
        var host = new DefaultShellHost(settings, []);
        _hostsToDispose.Add(host);

        // Act
        var shell = host.DefaultShell;

        // Assert
        Assert.Equal("Default", shell.Id.Name);
    }

    [Fact]
    public void DefaultShell_WithoutDefaultShell_ReturnsFirstShell()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new ShellId("First")),
            new ShellSettings(new ShellId("Second"))
        };
        var host = new DefaultShellHost(settings, []);
        _hostsToDispose.Add(host);

        // Act
        var shell = host.DefaultShell;

        // Assert
        Assert.Equal("First", shell.Id.Name);
    }

    [Fact]
    public void GetShell_WithValidId_ReturnsShellContext()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new ShellId("TestShell"))
        };
        var host = new DefaultShellHost(settings, []);
        _hostsToDispose.Add(host);

        // Act
        var shell = host.GetShell(new ShellId("TestShell"));

        // Assert
        Assert.NotNull(shell);
        Assert.Equal("TestShell", shell.Id.Name);
        Assert.NotNull(shell.Settings);
        Assert.NotNull(shell.ServiceProvider);
    }

    [Fact]
    public void GetShell_WithInvalidId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new ShellId("TestShell"))
        };
        var host = new DefaultShellHost(settings, []);
        _hostsToDispose.Add(host);

        // Act & Assert
        var ex = Assert.Throws<KeyNotFoundException>(() => host.GetShell(new ShellId("NonExistent")));
        Assert.Contains("NonExistent", ex.Message);
    }

    [Fact]
    public void GetShell_CalledMultipleTimes_ReturnsSameInstance()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new ShellId("TestShell"))
        };
        var host = new DefaultShellHost(settings, []);
        _hostsToDispose.Add(host);

        // Act
        var shell1 = host.GetShell(new ShellId("TestShell"));
        var shell2 = host.GetShell(new ShellId("TestShell"));

        // Assert
        Assert.Same(shell1, shell2);
    }

    [Fact]
    public void AllShells_ReturnsAllConfiguredShells()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new ShellId("Shell1")),
            new ShellSettings(new ShellId("Shell2")),
            new ShellSettings(new ShellId("Shell3"))
        };
        var host = new DefaultShellHost(settings, []);
        _hostsToDispose.Add(host);

        // Act
        var allShells = host.AllShells;

        // Assert
        Assert.Equal(3, allShells.Count);
        Assert.Contains(allShells, s => s.Id.Name == "Shell1");
        Assert.Contains(allShells, s => s.Id.Name == "Shell2");
        Assert.Contains(allShells, s => s.Id.Name == "Shell3");
    }

    [Fact]
    public void GetShell_WithEnabledFeatures_ResolvesAndConfiguresServices()
    {
        // Arrange
        var assembly = CreateTestAssembly(
            ("TestFeature", typeof(TestFeatureStartup), [])
        );
        var settings = new[]
        {
            new ShellSettings(new ShellId("TestShell"), ["TestFeature"])
        };
        var host = new DefaultShellHost(settings, [assembly]);
        _hostsToDispose.Add(host);

        // Act
        var shell = host.GetShell(new ShellId("TestShell"));

        // Assert
        Assert.NotNull(shell);
        var testService = shell.ServiceProvider.GetService<ITestService>();
        Assert.NotNull(testService);
    }

    [Fact]
    public void GetShell_WithFeatureDependencies_ConfiguresInCorrectOrder()
    {
        // Arrange
        var assembly = CreateTestAssemblyWithDependencies();
        var settings = new[]
        {
            new ShellSettings(new ShellId("TestShell"), ["ChildFeature"])
        };
        var host = new DefaultShellHost(settings, [assembly]);
        _hostsToDispose.Add(host);

        // Act
        var shell = host.GetShell(new ShellId("TestShell"));

        // Assert - Both services should be registered (parent dependency is auto-resolved)
        var parentService = shell.ServiceProvider.GetService<IParentService>();
        var childService = shell.ServiceProvider.GetService<IChildService>();
        Assert.NotNull(parentService);
        Assert.NotNull(childService);
    }

    [Fact]
    public void GetShell_WithUnknownFeature_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new ShellId("TestShell"), ["UnknownFeature"])
        };
        var host = new DefaultShellHost(settings, []);
        _hostsToDispose.Add(host);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => host.GetShell(new ShellId("TestShell")));
        Assert.Contains("UnknownFeature", ex.Message);
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void GetShell_ShellSettingsIsRegistered_ReturnsFromServiceProvider()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new ShellId("TestShell"))
        };
        var host = new DefaultShellHost(settings, []);
        _hostsToDispose.Add(host);

        // Act
        var shell = host.GetShell(new ShellId("TestShell"));
        var resolvedSettings = shell.ServiceProvider.GetService<ShellSettings>();

        // Assert
        Assert.NotNull(resolvedSettings);
        Assert.Equal("TestShell", resolvedSettings.Id.Name);
    }

    [Fact]
    public void GetShell_ShellContextIsRegistered_ReturnsFromServiceProvider()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new ShellId("TestShell"))
        };
        var host = new DefaultShellHost(settings, []);
        _hostsToDispose.Add(host);

        // Act
        var shell = host.GetShell(new ShellId("TestShell"));
        var resolvedContext = shell.ServiceProvider.GetRequiredService<ShellContext>();

        // Assert
        Assert.NotNull(resolvedContext);
        Assert.Same(shell, resolvedContext);
        Assert.Equal("TestShell", resolvedContext.Id.Name);
    }

    [Fact]
    public void Dispose_DisposesServiceProviders()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new ShellId("TestShell"))
        };
        var host = new DefaultShellHost(settings, []);
        _ = host.GetShell(new ShellId("TestShell")); // Ensure the shell is built

        // Act
        host.Dispose();

        // Assert - After dispose, accessing shells should throw
        Assert.Throws<ObjectDisposedException>(() => host.DefaultShell);
        Assert.Throws<ObjectDisposedException>(() => host.GetShell(new ShellId("TestShell")));
        Assert.Throws<ObjectDisposedException>(() => _ = host.AllShells);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new ShellId("TestShell"))
        };
        var host = new DefaultShellHost(settings, []);

        // Act & Assert - Should not throw
        host.Dispose();
        host.Dispose();
    }

    // Helper interfaces and test implementations
    public interface ITestService { }
    public class TestService : ITestService { }

    public interface IParentService { }
    public class ParentService : IParentService { }

    public interface IChildService { }
    public class ChildService : IChildService { }

    // Helper class for test features - cannot be used with dynamic assembly
    // but keeping for reference
    public class TestFeatureStartup : IShellStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ITestService, TestService>();
        }
    }

    /// <summary>
    /// Creates a dynamic assembly with test feature types for testing purposes.
    /// </summary>
    private static Assembly CreateTestAssembly(params (string FeatureName, Type StartupType, string[] Dependencies)[] featureDefinitions)
    {
        var assemblyName = new AssemblyName($"TestAssembly_{Guid.NewGuid():N}");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var uniqueCounter = 0;
        foreach (var (featureName, startupType, dependencies) in featureDefinitions)
        {
            var typeName = $"{featureName}Startup_{uniqueCounter++}";

            // Create the type implementing IShellStartup
            var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
            typeBuilder.AddInterfaceImplementation(typeof(IShellStartup));

            // Set static field value
            if (startupType == typeof(TestFeatureStartup))
            {
                // Will configure ITestService
                DefineConfigureServicesWithTestService(typeBuilder, typeof(ITestService), typeof(TestService));
            }
            else
            {
                // Implement ConfigureServices method with empty body
                var configureServicesMethod = typeBuilder.DefineMethod(
                    "ConfigureServices",
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(void),
                    [typeof(IServiceCollection)]);
                var ilConfigureServices = configureServicesMethod.GetILGenerator();
                ilConfigureServices.Emit(OpCodes.Ret);
            }

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

        return assemblyBuilder;
    }

    private static void DefineConfigureServicesWithTestService(TypeBuilder typeBuilder, Type serviceInterface, Type serviceImplementation)
    {
        // Implement ConfigureServices method that registers a service
        var configureServicesMethod = typeBuilder.DefineMethod(
            "ConfigureServices",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            [typeof(IServiceCollection)]);

        var il = configureServicesMethod.GetILGenerator();

        // Load the services collection
        il.Emit(OpCodes.Ldarg_1);

        // Call ServiceCollectionServiceExtensions.AddSingleton<TService, TImplementation>(services)
        var addSingletonMethod = typeof(ServiceCollectionServiceExtensions)
            .GetMethods()
            .First(m => m.Name == "AddSingleton" &&
                        m.IsGenericMethod &&
                        m.GetGenericArguments().Length == 2 &&
                        m.GetParameters().Length == 1)
            .MakeGenericMethod(serviceInterface, serviceImplementation);

        il.Emit(OpCodes.Call, addSingletonMethod);
        il.Emit(OpCodes.Pop); // Pop the return value (IServiceCollection)
        il.Emit(OpCodes.Ret);
    }

    private static Assembly CreateTestAssemblyWithDependencies()
    {
        var assemblyName = new AssemblyName($"TestAssembly_{Guid.NewGuid():N}");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        // Create ParentFeature startup
        CreateFeatureType(moduleBuilder, "ParentFeature", [], typeof(IParentService), typeof(ParentService));

        // Create ChildFeature startup with dependency on ParentFeature
        CreateFeatureType(moduleBuilder, "ChildFeature", ["ParentFeature"], typeof(IChildService), typeof(ChildService));

        return assemblyBuilder;
    }

    private static void CreateFeatureType(ModuleBuilder moduleBuilder, string featureName, string[] dependencies, Type serviceInterface, Type serviceImplementation)
    {
        var typeName = $"{featureName}Startup";
        var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
        typeBuilder.AddInterfaceImplementation(typeof(IShellStartup));

        // Implement ConfigureServices method that registers a service
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
