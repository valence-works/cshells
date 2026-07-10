using Microsoft.Extensions.DependencyInjection;

namespace CShells.Lifecycle;

/// <summary>
/// Performs ordered teardown work for a shell during graceful drain, after the shell has
/// transitioned to <see cref="ShellLifecycleState.Drained"/> and before its service provider
/// is disposed.
/// </summary>
/// <remarks>
/// The shell container is fully usable during termination: terminators are resolved from a
/// scope of the shell's provider and may resolve any shell service. Register implementations
/// via <c>IShellFeature.ConfigureServices</c> using
/// <see cref="ServiceCollectionLifecycleExtensions.AddShellTerminator{TTerminator}(IServiceCollection, LifecyclePhase, int)"/>
/// or <see cref="LifecycleOrderAttribute"/>. Terminators run sequentially in mirror-reversed
/// lifecycle order — <see cref="LifecyclePhase.Start"/> first, <see cref="LifecyclePhase.Prepare"/>
/// last, descending order within a phase — so a terminator registered with the same phase and
/// order as an initializer tears down at the mirrored point. A terminator that throws is logged
/// and does not prevent the remaining terminators from running. Terminators do not run on the
/// emergency-dispose path (host shutdown-timeout breach); singleton
/// <see cref="IDisposable"/>/<see cref="IAsyncDisposable"/> remains the last-resort cleanup.
/// </remarks>
public interface IShellTerminator
{
    /// <summary>Performs termination work.</summary>
    Task TerminateAsync(CancellationToken cancellationToken = default);
}
