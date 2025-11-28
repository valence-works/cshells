using CShells.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for <see cref="ApplicationBuilderExtensions"/>.
/// </summary>
public class ApplicationBuilderExtensionsTests
{
    [Fact(DisplayName = "UseCShells with null app throws ArgumentNullException")]
    public void UseCShells_WithNullApp_ThrowsArgumentNullException()
    {
        // Act & Assert
        IApplicationBuilder app = null!;
        var ex = Assert.Throws<ArgumentNullException>(() => app.UseCShells());
        Assert.Equal("app", ex.ParamName);
    }

    [Fact(DisplayName = "UseCShells returns app for chaining")]
    public void UseCShells_ReturnsAppForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IShellResolver, NullShellResolver>();
        services.AddSingleton<IShellHost, EmptyShellHost>();
        var serviceProvider = services.BuildServiceProvider();
        var app = new TestApplicationBuilder(serviceProvider);

        // Act
        var result = app.UseCShells();

        // Assert
        Assert.Same(app, result);
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

    private class TestApplicationBuilder : IApplicationBuilder
    {
        private readonly List<Func<RequestDelegate, RequestDelegate>> _components = [];

        public TestApplicationBuilder(IServiceProvider serviceProvider)
        {
            ApplicationServices = serviceProvider;
        }

        public IServiceProvider ApplicationServices { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public IFeatureCollection ServerFeatures => throw new NotImplementedException();

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
