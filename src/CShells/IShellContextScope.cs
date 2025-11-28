namespace CShells;

/// <summary>
/// Represents a scoped service provider for a shell context.
/// Provides a way to create scoped services within a shell's service provider.
/// </summary>
public interface IShellContextScope : IDisposable
{
    /// <summary>
    /// Gets the shell context this scope belongs to.
    /// </summary>
    ShellContext ShellContext { get; }

    /// <summary>
    /// Gets the scoped service provider for this shell context scope.
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}
