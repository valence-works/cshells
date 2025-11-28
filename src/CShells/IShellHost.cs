namespace CShells;

/// <summary>
/// Provides access to shell contexts and their configurations.
/// </summary>
public interface IShellHost
{
    /// <summary>
    /// Gets the default shell context. Returns the shell with Id "Default" if present,
    /// otherwise returns the first shell.
    /// </summary>
    ShellContext DefaultShell { get; }

    /// <summary>
    /// Gets a shell context by its identifier.
    /// </summary>
    /// <param name="id">The shell identifier.</param>
    /// <returns>The shell context for the specified identifier.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no shell with the specified identifier exists.</exception>
    ShellContext GetShell(ShellId id);

    /// <summary>
    /// Gets all available shell contexts.
    /// </summary>
    IReadOnlyCollection<ShellContext> AllShells { get; }
}
