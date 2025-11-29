using CShells.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for <see cref="IWebShellFeature"/> configuration in <see cref="ApplicationBuilderExtensions.UseCShells"/>.
/// </summary>
public class WebShellFeatureConfigurationTests : IDisposable
{
    /// <summary>
    /// Initializes a new test instance, resetting all static state.
    /// </summary>
    public WebShellFeatureConfigurationTests()
    {
        // Reset the static flag in ApplicationBuilderExtensions to allow testing
        ResetWebShellFeaturesConfiguredFlag();
        
        // Reset all test feature counters
        TestWebShellFeature.ResetConfigureCallCount();
        NonWebShellFeature.ResetConfigureCallCount();
        OrderedWebShellFeatureA.ResetCallOrder();
        OrderedWebShellFeatureB.ResetCallOrder();
        OrderedWebShellFeatureC.ResetCallOrder();
        OrderedFeatureCallOrderCounter.Reset();
    }

    /// <summary>
    /// Cleans up test state.
    /// </summary>
    public void Dispose()
    {
        // Reset the flag after each test to ensure clean state for other tests
        ResetWebShellFeaturesConfiguredFlag();
    }

    /// <summary>
    /// Resets the internal _webShellFeaturesConfigured flag using reflection.
    /// This is necessary for test isolation since the flag is static.
    /// </summary>
    private static void ResetWebShellFeaturesConfiguredFlag()
    {
        var field = typeof(CShells.AspNetCore.ApplicationBuilderExtensions)
            .GetField("_webShellFeaturesConfigured", BindingFlags.NonPublic | BindingFlags.Static);
        field?.SetValue(null, false);
    }
    /// <summary>
    /// Test that <see cref="IWebShellFeature.Configure"/> is invoked when <c>UseCShells()</c> is called.
    /// </summary>
    [Fact(DisplayName = "UseCShells invokes IWebShellFeature.Configure for discovered features")]
    public void UseCShells_InvokesConfigureOnWebShellFeatures()
    {
        // Arrange
        TestWebShellFeature.ResetConfigureCallCount();
        
        var services = new ServiceCollection();
        services.AddSingleton<IShellResolver, NullShellResolver>();
        services.AddSingleton<IShellHost, EmptyShellHost>();
        services.AddSingleton<IHostEnvironment, TestHostEnvironment>();
        var serviceProvider = services.BuildServiceProvider();
        var app = new TestApplicationBuilder(serviceProvider);

        // Act
        app.UseCShells();

        // Assert - the feature's Configure method should have been called
        // (assuming TestWebShellFeature is discovered from loaded assemblies)
        // Note: Since this is a test environment, the actual feature discovery
        // will find TestWebShellFeature from this test assembly.
        Assert.True(TestWebShellFeature.ConfigureCallCount > 0,
            "IWebShellFeature.Configure should have been called at least once");
    }

    /// <summary>
    /// Test that calling <c>UseCShells()</c> multiple times only configures features once.
    /// </summary>
    [Fact(DisplayName = "UseCShells only configures IWebShellFeature once even if called multiple times")]
    public void UseCShells_CalledMultipleTimes_ConfiguresFeaturesOnlyOnce()
    {
        // Arrange
        TestWebShellFeature.ResetConfigureCallCount();
        
        var services = new ServiceCollection();
        services.AddSingleton<IShellResolver, NullShellResolver>();
        services.AddSingleton<IShellHost, EmptyShellHost>();
        services.AddSingleton<IHostEnvironment, TestHostEnvironment>();
        var serviceProvider = services.BuildServiceProvider();
        var app = new TestApplicationBuilder(serviceProvider);

        // Act - call UseCShells() multiple times
        app.UseCShells();
        var firstCallCount = TestWebShellFeature.ConfigureCallCount;
        
        app.UseCShells();
        var secondCallCount = TestWebShellFeature.ConfigureCallCount;
        
        app.UseCShells();
        var thirdCallCount = TestWebShellFeature.ConfigureCallCount;

        // Assert - Configure should only be called once (in the first UseCShells call)
        Assert.Equal(firstCallCount, secondCallCount);
        Assert.Equal(firstCallCount, thirdCallCount);
    }

    /// <summary>
    /// Test that multiple <see cref="IWebShellFeature"/> implementations are configured in deterministic order.
    /// </summary>
    [Fact(DisplayName = "UseCShells configures IWebShellFeatures in deterministic order by feature name")]
    public void UseCShells_ConfiguresFeaturesInDeterministicOrder()
    {
        // Arrange
        OrderedWebShellFeatureA.ResetCallOrder();
        OrderedWebShellFeatureB.ResetCallOrder();
        OrderedWebShellFeatureC.ResetCallOrder();
        
        var services = new ServiceCollection();
        services.AddSingleton<IShellResolver, NullShellResolver>();
        services.AddSingleton<IShellHost, EmptyShellHost>();
        services.AddSingleton<IHostEnvironment, TestHostEnvironment>();
        var serviceProvider = services.BuildServiceProvider();
        var app = new TestApplicationBuilder(serviceProvider);

        // Act
        app.UseCShells();

        // Assert - features should be configured in alphabetical order by feature Id
        // Order should be: AAA_OrderedFeature, BBB_OrderedFeature, CCC_OrderedFeature (since TestWebShellFeature is also there)
        // Actually, we need to check the relative ordering
        var orderA = OrderedWebShellFeatureA.CallOrder;
        var orderB = OrderedWebShellFeatureB.CallOrder;
        var orderC = OrderedWebShellFeatureC.CallOrder;

        // AAA should come before BBB, BBB should come before CCC
        Assert.True(orderA < orderB, $"AAA_OrderedFeature (order {orderA}) should be configured before BBB_OrderedFeature (order {orderB})");
        Assert.True(orderB < orderC, $"BBB_OrderedFeature (order {orderB}) should be configured before CCC_OrderedFeature (order {orderC})");
    }

    /// <summary>
    /// Test that features implementing only <see cref="IShellFeature"/> (not <see cref="IWebShellFeature"/>) are not configured by <c>UseCShells()</c>.
    /// </summary>
    [Fact(DisplayName = "UseCShells does not invoke Configure on features that only implement IShellFeature")]
    public void UseCShells_DoesNotConfigureNonWebShellFeatures()
    {
        // Arrange
        NonWebShellFeature.ResetConfigureCallCount();
        
        var services = new ServiceCollection();
        services.AddSingleton<IShellResolver, NullShellResolver>();
        services.AddSingleton<IShellHost, EmptyShellHost>();
        services.AddSingleton<IHostEnvironment, TestHostEnvironment>();
        var serviceProvider = services.BuildServiceProvider();
        var app = new TestApplicationBuilder(serviceProvider);

        // Act
        app.UseCShells();

        // Assert - NonWebShellFeature does not implement IWebShellFeature, so it should not be configured
        Assert.Equal(0, NonWebShellFeature.ConfigureServicesCallCount);
    }

    // Test helpers
    private class NullShellResolver : IShellResolver
    {
        public ShellId? Resolve(HttpContext httpContext) => null;
    }

    private class EmptyShellHost : IShellHost
    {
        public ShellContext DefaultShell => throw new InvalidOperationException();
        public IReadOnlyCollection<ShellContext> AllShells => [];
        public ShellContext GetShell(ShellId id) => throw new KeyNotFoundException();
    }

    private class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private class TestApplicationBuilder : IApplicationBuilder
    {
        private readonly List<Func<RequestDelegate, RequestDelegate>> _components = [];

        public TestApplicationBuilder(IServiceProvider serviceProvider)
        {
            ApplicationServices = serviceProvider;
        }

        public IServiceProvider ApplicationServices { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public IFeatureCollection ServerFeatures => new FeatureCollection();

        public RequestDelegate Build()
        {
            RequestDelegate app = context => Task.CompletedTask;
            for (var i = _components.Count - 1; i >= 0; i--)
            {
                app = _components[i](app);
            }
            return app;
        }

        public IApplicationBuilder New() => new TestApplicationBuilder(ApplicationServices);

        public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware)
        {
            _components.Add(middleware);
            return this;
        }
    }
}

/// <summary>
/// A test web shell feature used for verifying <c>UseCShells()</c> behavior.
/// </summary>
[ShellFeature("TestWebShellFeature", DisplayName = "Test Web Shell Feature")]
public class TestWebShellFeature : IWebShellFeature
{
    private static int _configureCallCount;
    private static readonly object _lock = new();

    public static int ConfigureCallCount
    {
        get { lock (_lock) return _configureCallCount; }
    }

    public static void ResetConfigureCallCount()
    {
        lock (_lock) _configureCallCount = 0;
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // No-op for this test feature
    }

    /// <inheritdoc />
    public void Configure(IApplicationBuilder app, IHostEnvironment environment)
    {
        IncrementConfigureCallCount();
    }

    private static void IncrementConfigureCallCount()
    {
        lock (_lock) _configureCallCount++;
    }
}

/// <summary>
/// A test feature that only implements <see cref="IShellFeature"/>, not <see cref="IWebShellFeature"/>.
/// Used to verify that non-web features are not configured by <c>UseCShells()</c>.
/// </summary>
[ShellFeature("NonWebShellFeature", DisplayName = "Non-Web Shell Feature")]
public class NonWebShellFeature : IShellFeature
{
    private static int _configureServicesCallCount;
    private static readonly object _lock = new();

    public static int ConfigureServicesCallCount
    {
        get { lock (_lock) return _configureServicesCallCount; }
    }

    public static void ResetConfigureCallCount()
    {
        lock (_lock) _configureServicesCallCount = 0;
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        lock (_lock) _configureServicesCallCount++;
    }
}

// Global call order counter for ordered features
internal static class OrderedFeatureCallOrderCounter
{
    private static int _counter;
    private static readonly object _lock = new();

    public static int GetNextOrder()
    {
        lock (_lock) return ++_counter;
    }

    public static void Reset()
    {
        lock (_lock) _counter = 0;
    }
}

/// <summary>
/// Test feature A for ordering verification (should be configured first due to alphabetical ordering).
/// </summary>
[ShellFeature("AAA_OrderedFeature", DisplayName = "AAA Ordered Feature")]
public class OrderedWebShellFeatureA : IWebShellFeature
{
    private static int _callOrder;
    private static readonly object _lock = new();

    public static int CallOrder
    {
        get { lock (_lock) return _callOrder; }
    }

    public static void ResetCallOrder()
    {
        lock (_lock) _callOrder = 0;
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) { }

    /// <inheritdoc />
    public void Configure(IApplicationBuilder app, IHostEnvironment environment)
    {
        lock (_lock) _callOrder = OrderedFeatureCallOrderCounter.GetNextOrder();
    }
}

/// <summary>
/// Test feature B for ordering verification (should be configured second).
/// </summary>
[ShellFeature("BBB_OrderedFeature", DisplayName = "BBB Ordered Feature")]
public class OrderedWebShellFeatureB : IWebShellFeature
{
    private static int _callOrder;
    private static readonly object _lock = new();

    public static int CallOrder
    {
        get { lock (_lock) return _callOrder; }
    }

    public static void ResetCallOrder()
    {
        lock (_lock) _callOrder = 0;
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) { }

    /// <inheritdoc />
    public void Configure(IApplicationBuilder app, IHostEnvironment environment)
    {
        lock (_lock) _callOrder = OrderedFeatureCallOrderCounter.GetNextOrder();
    }
}

/// <summary>
/// Test feature C for ordering verification (should be configured third).
/// </summary>
[ShellFeature("CCC_OrderedFeature", DisplayName = "CCC Ordered Feature")]
public class OrderedWebShellFeatureC : IWebShellFeature
{
    private static int _callOrder;
    private static readonly object _lock = new();

    public static int CallOrder
    {
        get { lock (_lock) return _callOrder; }
    }

    public static void ResetCallOrder()
    {
        lock (_lock) _callOrder = 0;
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) { }

    /// <inheritdoc />
    public void Configure(IApplicationBuilder app, IHostEnvironment environment)
    {
        SetCallOrder();
    }

    private static void SetCallOrder()
    {
        lock (_lock) _callOrder = OrderedFeatureCallOrderCounter.GetNextOrder();
    }
}
