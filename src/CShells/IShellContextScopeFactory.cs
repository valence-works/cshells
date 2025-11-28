namespace CShells;

/// <summary>
/// Factory for creating <see cref="IShellContextScope"/> instances.
/// </summary>
public interface IShellContextScopeFactory
{
    /// <summary>
    /// Creates a new scope for the specified shell context.
    /// </summary>
    /// <param name="shellContext">The shell context to create a scope for.</param>
    /// <returns>A new <see cref="IShellContextScope"/> for the specified shell context.</returns>
    IShellContextScope CreateScope(ShellContext shellContext);
}
