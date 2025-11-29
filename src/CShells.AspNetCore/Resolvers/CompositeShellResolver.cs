using Microsoft.AspNetCore.Http;

namespace CShells.AspNetCore.Resolvers;

/// <summary>
/// A composite shell resolver that tries multiple <see cref="IShellResolver"/> instances in order
/// and returns the first non-null <see cref="ShellId"/>.
/// </summary>
public sealed class CompositeShellResolver : IShellResolver
{
    private readonly IReadOnlyList<IShellResolver> _resolvers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeShellResolver"/> class.
    /// </summary>
    /// <param name="resolvers">The ordered collection of resolvers to evaluate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resolvers"/> is null.</exception>
    public CompositeShellResolver(params IShellResolver[] resolvers)
    {
        ArgumentNullException.ThrowIfNull(resolvers);
        _resolvers = resolvers;
    }

    /// <inheritdoc />
    public ShellId? Resolve(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        foreach (var resolver in _resolvers)
        {
            var shellId = resolver.Resolve(httpContext);
            if (shellId.HasValue)
            {
                return shellId;
            }
        }

        return null;
    }
}

