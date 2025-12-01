using CShells.Hosting;
using CShells.Resolution;
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
    public static IEnumerable<object[]> GuardClauseData() => new[]
    {
        new object?[] { null, "app" }
    };

    [Theory(DisplayName = "UseCShells guard clauses throw ArgumentNullException")]
    [MemberData(nameof(GuardClauseData))]
    public void UseCShells_GuardClauses_ThrowArgumentNullException(IApplicationBuilder? app, string expectedParam)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => CShells.AspNetCore.Extensions.ApplicationBuilderExtensions.UseCShells(app!));
        Assert.Equal(expectedParam, exception.ParamName);
    }

    [Fact(DisplayName = "UseCShells returns app for chaining")]
    public void UseCShells_ReturnsAppForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IShellResolver, NullShellResolver>();
        services.AddSingleton<CShells.Features.IShellFeatureFactory, CShells.Features.DefaultShellFeatureFactory>();
        services.AddSingleton<IShellHost, EmptyShellHost>();
        var serviceProvider = services.BuildServiceProvider();
        var app = new TestApplicationBuilder(serviceProvider);

        // Act
        var result = CShells.AspNetCore.Extensions.ApplicationBuilderExtensions.UseCShells(app);

        // Assert
        Assert.Same(app, result);
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

    private class TestApplicationBuilder(IServiceProvider serviceProvider) : IApplicationBuilder
    {
        private readonly List<Func<RequestDelegate, RequestDelegate>> _components = [];

        public IServiceProvider ApplicationServices { get; set; } = serviceProvider;
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
