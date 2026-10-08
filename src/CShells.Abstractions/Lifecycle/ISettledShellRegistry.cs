namespace CShells.Lifecycle;

/// <summary>
/// Optionally observes a registry's current active shell only after its activation has settled successfully.
/// </summary>
/// <remarks>
/// The built-in registry implements this capability on the same object as <see cref="IShellRegistry"/>. Resolve the
/// existing registry and cast it; a separate service registration is not promised. Custom registries need not implement
/// this interface. A missing capability is therefore distinct from a supported observation that returns <see langword="null"/>.
/// This is a point-in-time observation, not a lease: the returned shell may begin draining immediately afterward.
/// </remarks>
public interface ISettledShellRegistry
{
    /// <summary>Returns the current settled active generation for <paramref name="name"/>, or <see langword="null"/>.</summary>
    /// <param name="name">The shell name, matched case insensitively by the built-in registry.</param>
    /// <returns>The exact current generation after activation completion callbacks and eligibility checks, or <see langword="null"/> when none is settled.</returns>
    /// <remarks>
    /// This synchronous observation does not discover blueprints, activate or reload a shell, resolve its provider, invoke callbacks,
    /// or wait for activation serialization. During a provisional replacement it returns <see langword="null"/> rather than
    /// falling back to a historical generation. Completion-callback failures retain their existing diagnostic-only behavior.
    /// Routing visibility through <see cref="IShellRegistry.GetActive(string)"/> and startup-run terminal semantics are unchanged.
    /// Invalid names throw <see cref="ArgumentException"/> in the built-in registry.
    /// </remarks>
    IShell? GetSettledActive(string name);
}
