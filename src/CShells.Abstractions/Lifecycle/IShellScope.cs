namespace CShells.Lifecycle;

/// <summary>
/// A tracked DI scope obtained from <see cref="IShell.BeginScope"/>. Outstanding scopes
/// delay the shell's drain handler-invocation phase until released (or the deadline elapses).
/// </summary>
/// <remarks>
/// The handle bundles two responsibilities: (1) it is an <see cref="IAsyncDisposable"/> wrapping
/// an <see cref="Microsoft.Extensions.DependencyInjection.IServiceScope"/> built from the shell's provider, and (2) disposing it
/// decrements the shell's active-scope counter so the registry can coordinate drain safely.
/// </remarks>
public interface IShellScope : IAsyncDisposable
{
    /// <summary>Gets the owning shell.</summary>
    IShell Shell { get; }

    /// <summary>Gets the scoped <see cref="IServiceProvider"/>.</summary>
    IServiceProvider ServiceProvider { get; }
}
