using CShells.Lifecycle;

namespace CShells.Nuplane;

/// <summary>Options for the optional Nuplane and CShells feature-discovery integration.</summary>
public sealed class NuplaneIntegrationOptions
{
    /// <summary>Gets or sets whether reconciliation callbacks may request refresh or automatic reload work.</summary>
    /// <remarks>The feature assembly provider remains available for normal catalog initialization when false.</remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the eligible-completion policy for refreshing the feature catalog.</summary>
    public NuplaneRefreshTrigger RefreshTrigger { get; set; } = NuplaneRefreshTrigger.ChangedOrPending;

    /// <summary>Gets or sets whether active shells reload after eligible catalog freshness work.</summary>
    public bool AutoReload { get; set; }

    /// <summary>
    /// Gets or sets an optional host-owned callback for reporting raw per-shell automatic-reload results.
    /// </summary>
    /// <remarks>
    /// The callback receives a stable read-only copy of the original results, including partial errors and their
    /// exception chains. It cannot change how the integration evaluates reload success. A callback failure leaves
    /// reload work pending, though shell generations already promoted by the registry are not rolled back.
    /// </remarks>
    public Func<IReadOnlyList<ReloadResult>, CancellationToken, ValueTask>? OnReloadResults { get; set; }
}
