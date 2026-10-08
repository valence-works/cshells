namespace CShells.Lifecycle;

/// <summary>
/// Begins optional host-owned protection for an attempted shell generation.
/// </summary>
/// <remarks>
/// Participants are resolved once from the root provider and called in registration order after blueprint
/// composition and name validation, but before CShells initializes or reads the runtime feature catalog. Callbacks
/// for different shell names may run concurrently. A callback must not reenter activation, reload, or unregister for
/// the same shell name because the registry serializes those operations with a non-reentrant per-name semaphore.
/// Root registrations whose service type, implementation type, or instance implements this contract are excluded
/// from shell providers. Factory registrations must expose a participant service type; CShells does not invoke
/// arbitrary factories to discover hidden participant implementations.
/// If a call throws before returning a lease, the participant must clean up its own partial acquisition. A failure
/// from a participant propagates unchanged. If one lease's snapshot callback fails, callbacks for later leases are
/// skipped, but CShells still releases every lease acquired for that attempt.
/// </remarks>
public interface IShellGenerationBuildParticipant
{
    /// <summary>
    /// Starts participation for the reserved shell-generation identity.
    /// </summary>
    /// <param name="context">The immutable descriptor and shell ID reserved before composition.</param>
    /// <param name="cancellationToken">A token that can cancel the build attempt.</param>
    /// <returns>A lease owned by this generation; return a no-op lease when no protection is needed.</returns>
    /// <exception cref="InvalidOperationException">The participant returned a null lease.</exception>
    ValueTask<IShellGenerationBuildLease> BeginAsync(
        ShellGenerationBuildContext context,
        CancellationToken cancellationToken = default);
}
