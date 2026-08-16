namespace CShells.Lifecycle;

/// <summary>
/// Participates in the prepare/commit boundary for a shell generation becoming active.
/// </summary>
/// <remarks>
/// Preparation runs before the Active lifecycle notification and must remain externally
/// invisible. Commit runs only after subscribers accept the generation and the registry makes
/// it exactly addressable. Rollback releases prepared state when either phase fails.
/// </remarks>
public interface IShellGenerationActivationParticipant
{
    /// <summary>Prepares generation-owned state without making it externally visible.</summary>
    Task PrepareAsync(IShell shell, CancellationToken cancellationToken = default);

    /// <summary>Commits prepared state after the registry publishes the generation.</summary>
    void Commit(IShell shell);

    /// <summary>
    /// Releases rollback state after every participant commits successfully. Completion is
    /// best-effort cleanup and must not be used as an additional acceptance phase.
    /// </summary>
    void Complete(IShell shell);

    /// <summary>
    /// Discards prepared or partially committed state. Implementations must tolerate rollback
    /// after their own preparation throws and repeated cleanup during shell disposal.
    /// </summary>
    void Rollback(IShell shell);
}
