namespace CShells.Lifecycle;

/// <summary>Outcome for a single <see cref="IShellTerminator"/> invocation.</summary>
/// <param name="TerminatorTypeName">The concrete terminator type's <c>.Name</c>.</param>
/// <param name="Completed">True if the terminator returned within its cancellation budget.</param>
/// <param name="Elapsed">Wall-clock time consumed by this terminator.</param>
/// <param name="Error">Non-null if the terminator threw.</param>
public sealed record ShellTerminatorResult(
    string TerminatorTypeName,
    bool Completed,
    TimeSpan Elapsed,
    Exception? Error);
