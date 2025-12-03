using CShells.Resolution;

namespace CShells.AspNetCore.Resolution;

/// <summary>
/// A resolver strategy that always returns a fixed shell identifier.
/// </summary>
public class FixedShellResolver(ShellId shellId) : IShellResolverStrategy
{
    public ShellId? Resolve(ShellResolutionContext context) => shellId;
}
