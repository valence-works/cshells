namespace CShells.Lifecycle;

/// <summary>
/// The authoritative index of <b>active</b> shell generations, backed by one or more
/// <see cref="IShellBlueprintProvider"/> instances that own the catalogue.
/// </summary>
/// <remarks>
/// <para>
/// The registry is a cache of live generations — it does NOT hold the blueprint catalogue.
/// Blueprints live in a single registered <see cref="IShellBlueprintProvider"/> (in-memory,
/// configuration-backed, storage-backed, or any first- or third-party implementation).
/// Activation is lazy: <see cref="GetOrActivateAsync"/> consults the provider on first touch
/// and caches the resulting <see cref="IShell"/>.
/// </para>
/// <para>
/// Each blueprint <see cref="IShell.Descriptor"/>.<c>Name</c> is served by at most one active
/// <see cref="IShell"/> at any moment — any number may concurrently be in
/// <see cref="ShellLifecycleState.Deactivating"/> / <see cref="ShellLifecycleState.Draining"/>
/// / <see cref="ShellLifecycleState.Drained"/> while their replacement is live.
/// </para>
/// <para>
/// Every activation entry point (<see cref="ActivateAsync"/>, <see cref="GetOrActivateAsync"/>,
/// <see cref="ReloadAsync"/>, <see cref="UnregisterBlueprintAsync"/>) serializes on a per-name
/// <c>SemaphoreSlim</c>, so concurrent callers observe deterministic ordering without any
/// possibility of two generations existing simultaneously in <see cref="ShellLifecycleState.Active"/>.
/// </para>
/// </remarks>
public interface IShellRegistry
{
    // =========================================================================
    // Activation
    // =========================================================================

    /// <summary>
    /// Returns the active generation for <paramref name="name"/> if present; otherwise
    /// performs a provider lookup, builds the next generation, runs its
    /// <see cref="IShellInitializer"/> services, promotes to <see cref="ShellLifecycleState.Active"/>,
    /// and returns it.
    /// </summary>
    /// <remarks>
    /// Concurrent calls for the same inactive name are serialized — exactly one provider
    /// lookup and one shell build is performed; all callers observe the same instance.
    /// </remarks>
    /// <exception cref="ShellBlueprintNotFoundException">The provider returned <c>null</c>.</exception>
    /// <exception cref="ShellBlueprintUnavailableException">The provider threw during lookup.</exception>
    Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Composes fresh settings from the registered blueprint, builds generation 1, runs its
    /// <see cref="IShellInitializer"/> services, and promotes it to <see cref="ShellLifecycleState.Active"/>.
    /// </summary>
    /// <exception cref="ShellBlueprintNotFoundException">The provider returned <c>null</c>.</exception>
    /// <exception cref="ShellBlueprintUnavailableException">The provider threw during lookup.</exception>
    /// <exception cref="InvalidOperationException">A shell for <paramref name="name"/> is already active — use <see cref="ReloadAsync"/> or <see cref="GetOrActivateAsync"/>.</exception>
    Task<IShell> ActivateAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Composes fresh settings from the registered blueprint, builds the next generation,
    /// runs initializers, promotes it to <see cref="ShellLifecycleState.Active"/>, and
    /// initiates cooperative drain on the previously-active generation.
    /// </summary>
    /// <remarks>
    /// If no generation is currently active, behaves equivalently to <see cref="ActivateAsync"/>.
    /// Concurrent calls for the same name are serialized.
    /// </remarks>
    /// <exception cref="ShellBlueprintNotFoundException">The provider returned <c>null</c>.</exception>
    /// <exception cref="ShellBlueprintUnavailableException">The provider threw during lookup.</exception>
    Task<ReloadResult> ReloadAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads every shell currently active in the registry. Inactive blueprints are left
    /// inactive — consistent with feature <c>007</c>'s lazy activation model. Per-shell
    /// outcomes are returned; a single failure does not abort the batch.
    /// </summary>
    /// <param name="options">Parallelism options; defaults to <see cref="ReloadOptions.MaxDegreeOfParallelism"/> = 8.</param>
    Task<IReadOnlyList<ReloadResult>> ReloadActiveAsync(ReloadOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a cooperative drain on <paramref name="shell"/>. Three phases: scope wait,
    /// parallel handler invocation, grace after deadline or force. Concurrent calls for the
    /// same shell return the same in-flight operation.
    /// </summary>
    Task<IDrainOperation> DrainAsync(IShell shell, CancellationToken cancellationToken = default);

    // =========================================================================
    // Unregister (catalogue + runtime)
    // =========================================================================

    /// <summary>
    /// Removes the blueprint for <paramref name="name"/> in two ordered phases:
    /// (1) invokes the owning manager's <see cref="IShellBlueprintManager.DeleteAsync"/> to
    /// persist the removal; (2) drains and disposes any active generation, then clears the
    /// in-memory slot.
    /// </summary>
    /// <exception cref="ShellBlueprintNotFoundException">The provider does not claim this name.</exception>
    /// <exception cref="BlueprintNotMutableException">The blueprint's source is read-only (no manager). No runtime state changes.</exception>
    Task UnregisterBlueprintAsync(string name, CancellationToken cancellationToken = default);

    // =========================================================================
    // Read access (delegates to provider)
    // =========================================================================

    /// <summary>
    /// Returns the blueprint registered for <paramref name="name"/> via the host's single
    /// <see cref="IShellBlueprintProvider"/>, without activating a shell. Returns <c>null</c>
    /// when the provider does not claim the name.
    /// </summary>
    /// <remarks>
    /// Provider exceptions propagate raw — this method does NOT wrap in
    /// <see cref="ShellBlueprintUnavailableException"/> because the method is consumed by admin
    /// read flows that want direct access to the underlying fault for diagnostics.
    /// </remarks>
    Task<ProvidedBlueprint?> GetBlueprintAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the manager associated with <paramref name="name"/>'s owning provider, or
    /// <c>null</c> when the source is read-only or the name is unknown.
    /// </summary>
    Task<IShellBlueprintManager?> GetManagerAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Paginated view of the catalogue left-joined with the registry's in-memory lifecycle
    /// state. Blueprints with no active generation appear with null lifecycle fields.
    /// </summary>
    Task<ShellPage> ListAsync(ShellListQuery query, CancellationToken cancellationToken = default);

    /// <summary>The single active shell for <paramref name="name"/>, or <c>null</c> if none.</summary>
    IShell? GetActive(string name);

    /// <summary>
    /// The generations currently retained for <paramref name="name"/>: the active generation plus any
    /// earlier generations still draining. A generation is released once it reaches
    /// <see cref="ShellLifecycleState.Disposed"/>, so fully torn-down generations are NOT returned —
    /// callers cannot rely on historical generations remaining available after teardown. Empty when the
    /// name has no retained generations.
    /// </summary>
    IReadOnlyCollection<IShell> GetAll(string name);

    /// <summary>
    /// Every currently-active shell across all names, in no particular order. Synchronous; reads
    /// the in-memory index only — does not consult the provider.
    /// </summary>
    IReadOnlyCollection<IShell> GetActiveShells();

    // =========================================================================
    // Lifecycle subscribers
    // =========================================================================

    /// <summary>Registers a lifecycle subscriber notified of every state transition.</summary>
    void Subscribe(IShellLifecycleSubscriber subscriber);

    /// <summary>Removes a previously-registered lifecycle subscriber.</summary>
    void Unsubscribe(IShellLifecycleSubscriber subscriber);
}
