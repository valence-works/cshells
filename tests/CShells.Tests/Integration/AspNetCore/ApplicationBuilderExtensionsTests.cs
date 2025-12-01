using CShells.AspNetCore.Routing;
using CShells.Hosting;
using CShells.Resolution;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for <see cref="ApplicationBuilderExtensions"/>.
/// </summary>
public class ApplicationBuilderExtensionsTests
{
    public static IEnumerable<object[]> GuardClauseData()
    {
        yield return new object[] { null!, "app" };
    }

    [Theory(DisplayName = "MapCShells guard clauses throw ArgumentNullException")]
    [MemberData(nameof(GuardClauseData))]
    public void MapCShells_GuardClauses_ThrowArgumentNullException(IApplicationBuilder? app, string expectedParam)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => CShells.AspNetCore.Extensions.ApplicationBuilderExtensions.MapShells(app!));
        Assert.Equal(expectedParam, exception.ParamName);
    }

    [Fact(DisplayName = "MapCShells configures middleware and endpoints")]
    public void MapCShells_ConfiguresMiddlewareAndEndpoints()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IShellResolver, NullShellResolver>();
        services.AddSingleton<CShells.Features.IShellFeatureFactory, CShells.Features.DefaultShellFeatureFactory>();
        services.AddSingleton<IShellHost, EmptyShellHost>();
        services.AddSingleton<EndpointRouteBuilderAccessor>();
        services.AddSingleton<DynamicShellEndpointDataSource>();
        var serviceProvider = services.BuildServiceProvider();
        var app = new TestApplicationBuilder(serviceProvider);

        // Act
        var result = CShells.AspNetCore.Extensions.ApplicationBuilderExtensions.MapShells(app);

        // Assert
        Assert.NotNull(result);
    }

    // Test helpers
    private class NullShellResolver : IShellResolver
    {
        public ShellId? Resolve(ShellResolutionContext context) => null;
    }

    private class EmptyShellHost : IShellHost
    {
        public ShellContext DefaultShell => throw new InvalidOperationException();
        public IReadOnlyCollection<ShellContext> AllShells => [];
        public ShellContext GetShell(ShellId id) => throw new KeyNotFoundException();
    }

    private class TestApplicationBuilder(IServiceProvider serviceProvider) : IApplicationBuilder, IEndpointRouteBuilder
    {
        private readonly List<Func<RequestDelegate, RequestDelegate>> _components = [];
        private readonly List<EndpointDataSource> _dataSources = [];

        public IServiceProvider ApplicationServices { get; set; } = serviceProvider;
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public IFeatureCollection ServerFeatures => throw new NotImplementedException();

        // IEndpointRouteBuilder implementation
        public IServiceProvider ServiceProvider => ApplicationServices;
        public ICollection<EndpointDataSource> DataSources => _dataSources;

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

        public IApplicationBuilder CreateApplicationBuilder() => new TestApplicationBuilder(ApplicationServices);
    }
}
