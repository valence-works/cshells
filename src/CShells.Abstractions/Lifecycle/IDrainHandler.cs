namespace CShells.Lifecycle;

/// <summary>
/// Performs cooperative drain work when a shell enters
/// <see cref="ShellLifecycleState.Draining"/>, after the scope-wait phase has completed (or
/// been bounded out by the drain deadline).
/// </summary>
/// <remarks>
/// Register implementations as transient services via <c>IShellFeature.ConfigureServices</c>.
/// All handlers on a draining shell are resolved from the shell's <see cref="IServiceProvider"/>
/// and invoked in parallel; lifecycle phase and order metadata does not apply to them. For
/// ordered teardown that runs after all drain handlers complete — while the shell container is
/// still fully usable — implement <see cref="IShellTerminator"/> instead.
/// </remarks>
public interface IDrainHandler
{
    /// <summary>Performs drain work for this handler.</summary>
    /// <param name="extensionHandle">
    /// Handle to request a deadline extension from the configured <see cref="IDrainPolicy"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancelled when the drain deadline elapses or <c>ForceAsync</c> is called. Handlers
    /// should observe this token and return promptly when cancelled.
    /// </param>
    Task DrainAsync(IDrainExtensionHandle extensionHandle, CancellationToken cancellationToken);
}
