using Microsoft.AspNetCore.Http;

namespace CShells.AspNetCore;

/// <summary>
/// Abstraction for resolving a <see cref="ShellId"/> from an <see cref="HttpContext"/>.
/// </summary>
public interface IShellResolver
{
    /// <summary>
    /// Resolves the shell identifier from the HTTP context.
    /// </summary>
    /// <param name="httpContext">The HTTP context for the current request.</param>
    /// <returns>The resolved <see cref="ShellId"/>, or <c>null</c> if no shell could be determined.</returns>
    ShellId? Resolve(HttpContext httpContext);
}
