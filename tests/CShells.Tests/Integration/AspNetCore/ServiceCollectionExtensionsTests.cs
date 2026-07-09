using CShells.DependencyInjection;
using CShells.Lifecycle;
using CShells.Resolution;
using CShells.Tests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for <see cref="CShells.AspNetCore.Extensions.ServiceCollectionExtensions"/>.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact(DisplayName = "AddCShellsAspNetCore registers default IShellResolver")]
    public void AddCShellsAspNetCore_RegistersDefaultResolver()
    {
        using var sp = BuildProvider();
        Assert.NotNull(sp.GetService<IShellResolver>());
    }

    [Fact(DisplayName = "AddCShellsAspNetCore default resolver returns null when no active shells exist")]
    public async Task AddCShellsAspNetCore_DefaultResolver_ReturnsNullWithoutShells()
    {
        using var sp = BuildProvider();
        var resolver = sp.GetRequiredService<IShellResolver>();

        var result = await resolver.ResolveAsync(new ShellResolutionContext());

        Assert.Null(result);
    }

    [Fact(DisplayName = "AddCShellsAspNetCore does not override a custom IShellResolver")]
    public void AddCShellsAspNetCore_WithCustomResolver_DoesNotOverride()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IShellResolver, CustomShellResolver>();
        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services);

        using var sp = services.BuildServiceProvider();

        Assert.IsType<CustomShellResolver>(sp.GetRequiredService<IShellResolver>());
    }

    [Fact(DisplayName = "AddCShellsAspNetCore with null services throws ArgumentNullException")]
    public void AddCShellsAspNetCore_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;
        var ex = Assert.Throws<ArgumentNullException>(() =>
            CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services!));
        Assert.Equal("services", ex.ParamName);
    }

    [Fact(DisplayName = "AddCShellsAspNetCore returns CShellsBuilder for chaining")]
    public void AddCShellsAspNetCore_ReturnsBuilderForChaining()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var result = CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services);

        Assert.IsType<CShellsBuilder>(result);
        Assert.Same(services, result.Services);
    }

    [Fact(DisplayName = "AddCShellsAspNetCore registers web-routing + default fallback + any custom strategies")]
    public void AddCShellsAspNetCore_RegistersStrategies()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IShellResolverStrategy, CustomStrategy>();
        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services);

        using var sp = services.BuildServiceProvider();
        var strategies = sp.GetServices<IShellResolverStrategy>().ToList();

        Assert.Contains(strategies, s => s is CustomStrategy);
        Assert.Contains(strategies, s => s.GetType().Name == "WebRoutingShellResolver");
        Assert.Contains(strategies, s => s is DefaultShellResolverStrategy);
    }

    [Fact(DisplayName = "DefaultShellResolver orchestrates strategies in order and returns the first non-null hit")]
    public async Task DefaultShellResolver_OrchestratesInOrder()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IShellResolverStrategy, NullStrategy>();
        services.AddSingleton<IShellResolverStrategy, CustomStrategy>();
        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services);

        using var sp = services.BuildServiceProvider();
        var resolver = sp.GetRequiredService<IShellResolver>();

        var result = await resolver.ResolveAsync(new ShellResolutionContext());

        Assert.NotNull(result);
        Assert.Equal(new ShellId("Custom"), result.Value);
    }

    [Fact(DisplayName = "AddCShellsAspNetCore invokes configure exactly once")]
    public void AddCShellsAspNetCore_CallsConfigureOnce()
    {
        var counter = 0;
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services, _ => counter++);

        Assert.Equal(1, counter);
    }

    [Fact(DisplayName = "AddCShellsAspNetCore registers a scoped IMiddlewareFactory when the host has none")]
    public void AddCShellsAspNetCore_RegistersScopedMiddlewareFactory()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IMiddlewareFactory));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(MiddlewareFactory), descriptor.ImplementationType);
    }

    [Fact(DisplayName = "AddCShellsAspNetCore does not override a host-registered IMiddlewareFactory")]
    public void AddCShellsAspNetCore_WithHostMiddlewareFactory_DoesNotOverride()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddScoped<IMiddlewareFactory, CustomMiddlewareFactory>();

        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services);

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        Assert.IsType<CustomMiddlewareFactory>(scope.ServiceProvider.GetRequiredService<IMiddlewareFactory>());
    }

    [Fact(DisplayName = "Shell scopes resolve IMiddlewareFactory even when the host never registered one")]
    public async Task ShellScope_ResolvesMiddlewareFactory_FromRootCopy()
    {
        // A plain ServiceCollection root (no web host) has no IMiddlewareFactory of its own;
        // the shell container must still carry the one AddCShellsAspNetCore registers,
        // via ShellProviderBuilder.CopyRootServices.
        var stub = new StubShellBlueprintProvider().Add("acme");
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services, cshells =>
        {
            cshells.WithAssemblies(); // no feature discovery
            cshells.AddBlueprintProvider(_ => stub);
        });
        await using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IShellRegistry>();

        var shell = await registry.GetOrActivateAsync("acme");
        await using var scope = shell.BeginScope();

        Assert.IsType<MiddlewareFactory>(scope.ServiceProvider.GetRequiredService<IMiddlewareFactory>());
    }

    [Fact(DisplayName = "Shell containers do not get a duplicate ShellMiddlewarePipelineRegistry")]
    public async Task ShellScope_DoesNotResolvePipelineRegistry()
    {
        // The registry is root-only dispatch infrastructure. Copying its descriptor into shell
        // containers would hand shell-scoped consumers a fresh, permanently-empty instance —
        // resolution from a shell scope must fail loudly (null) instead.
        var stub = new StubShellBlueprintProvider().Add("acme");
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services, cshells =>
        {
            cshells.WithAssemblies();
            cshells.AddBlueprintProvider(_ => stub);
        });
        await using var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<CShells.AspNetCore.Middleware.ShellMiddlewarePipelineRegistry>());

        var shell = await sp.GetRequiredService<IShellRegistry>().GetOrActivateAsync("acme");
        await using var scope = shell.BeginScope();

        Assert.Null(scope.ServiceProvider.GetService<CShells.AspNetCore.Middleware.ShellMiddlewarePipelineRegistry>());
    }

    private static ServiceProvider BuildProvider(Action<CShellsBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        CShells.AspNetCore.Extensions.ServiceCollectionExtensions.AddCShellsAspNetCore(services, configure);
        return services.BuildServiceProvider();
    }

    private sealed class CustomShellResolver : IShellResolver
    {
        public Task<ShellId?> ResolveAsync(ShellResolutionContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult<ShellId?>(new ShellId("Custom"));
    }

    private sealed class CustomStrategy : IShellResolverStrategy
    {
        public Task<ShellId?> ResolveAsync(ShellResolutionContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult<ShellId?>(new ShellId("Custom"));
    }

    private sealed class NullStrategy : IShellResolverStrategy
    {
        public Task<ShellId?> ResolveAsync(ShellResolutionContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult<ShellId?>(null);
    }

    private sealed class CustomMiddlewareFactory : IMiddlewareFactory
    {
        public IMiddleware? Create(Type middlewareType) => null;
        public void Release(IMiddleware middleware) { }
    }
}
