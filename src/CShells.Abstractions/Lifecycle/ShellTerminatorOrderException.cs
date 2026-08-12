namespace CShells.Lifecycle;

/// <summary>
/// Thrown when shell terminator ordering metadata cannot produce a valid termination plan.
/// </summary>
public sealed class ShellTerminatorOrderException : InvalidOperationException
{
    /// <summary>
    /// Creates a new <see cref="ShellTerminatorOrderException"/> for the specified shell.
    /// </summary>
    /// <param name="shell">The shell descriptor being terminated.</param>
    /// <param name="message">The ordering validation failure.</param>
    public ShellTerminatorOrderException(ShellDescriptor shell, string message)
        : base($"Invalid shell terminator ordering for shell '{shell.Name}' generation {shell.Generation}: {message}")
    {
        Shell = shell;
    }

    /// <summary>
    /// Gets the shell descriptor whose terminator ordering failed validation.
    /// </summary>
    public ShellDescriptor Shell { get; }
}
