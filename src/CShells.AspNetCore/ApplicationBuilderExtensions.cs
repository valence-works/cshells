using Microsoft.AspNetCore.Builder;

namespace CShells.AspNetCore;

/// <summary>
/// Extension methods for configuring CShells middleware in the ASP.NET Core pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the CShells middleware to the application pipeline.
    /// This middleware resolves the current shell for each request and sets the
    /// appropriate service scope.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseCShells(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ShellMiddleware>();
    }
}
