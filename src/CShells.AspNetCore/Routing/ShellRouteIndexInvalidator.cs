using CShells.Lifecycle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.AspNetCore.Routing;

/// <summary>
/// Subscribes to <see cref="IShellRegistry"/> lifecycle events and invalidates the
/// <see cref="DefaultShellRouteIndex"/> snapshot whenever a transition suggests routing
/// metadata may have changed.
/// </summary>
/// <remarks>
/// <para>
/// Registration happens in
/// <see cref="CShells.AspNetCore.Extensions.ServiceCollectionExtensions"/> after the registry
/// singleton is constructed. The subscription itself is performed by the companion
/// <see cref="ShellRouteIndexInvalidatorHostedService"/>, which calls
/// <see cref="IShellRegistry.Subscribe"/> on host start so this subscriber observes every
/// transition from process start.
/// </para>
/// <para>
/// We invalidate on transitions that may change the snapshot's routing metadata:
/// </para>
/// <list type="bullet">
///   <item><see cref="ShellLifecycleState.Initializing"/> for a name NOT yet known to the
///         snapshot — a brand-new blueprint just appeared (e.g., created via the management
///         API and now activated for the first time).</item>
///   <item><see cref="ShellLifecycleState.Disposed"/> — a generation has gone away; the
///         underlying blueprint may have been removed (or its config replaced via reload),
///         so we re-read the catalogue.</item>
/// </list>
/// <para>
/// We deliberately do NOT invalidate on <see cref="ShellLifecycleState.Initializing"/> for
/// a name already present in the snapshot — that's the routine lazy-activation path for an
/// existing blueprint, and the routing metadata didn't change. Doing so would force a
/// <c>provider.ListAsync</c> rescan on every first request to every shell, which would
/// negate the snapshot's caching benefit at scale.
/// </para>
/// <para>
/// Subscriber-isolation per Constitution Principle VII: if invalidation throws, we log and
/// swallow so we never block the registry's notification fan-out for other subscribers.
/// </para>
/// </remarks>
internal sealed class ShellRouteIndexInvalidator(
    IShellRouteIndex routeIndex,
    ILogger<ShellRouteIndexInvalidator>? logger = null) : IShellLifecycleSubscriber
{
    private readonly IShellRouteIndex _routeIndex = Guard.Against.Null(routeIndex);
    private readonly ILogger<ShellRouteIndexInvalidator> _logger = logger ?? NullLogger<ShellRouteIndexInvalidator>.Instance;

    /// <inheritdoc />
    public Task OnStateChangedAsync(
        IShell shell,
        ShellLifecycleState previous,
        ShellLifecycleState current,
        CancellationToken cancellationToken = default)
    {
        if (current is not (ShellLifecycleState.Initializing or ShellLifecycleState.Disposed))
            return Task.CompletedTask;

        try
        {
            if (_routeIndex is not DefaultShellRouteIndex defaultIndex)
                return Task.CompletedTask;

            // Initializing for a name already in the snapshot is the routine lazy-activation
            // path for an existing blueprint — nothing to invalidate. Skipping here keeps
            // first-request activation O(1) on the snapshot side and avoids per-shell
            // provider.ListAsync rescans.
            if (current == ShellLifecycleState.Initializing
                && defaultIndex.ContainsShellName(shell.Descriptor.Name))
            {
                return Task.CompletedTask;
            }

            defaultIndex.Invalidate();
            _logger.LogDebug(
                "Invalidated route-index snapshot in response to '{ShellName}' transition {Previous} → {Current}.",
                shell.Descriptor.Name, previous, current);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Route-index invalidation threw for '{ShellName}' ({Previous} → {Current}); swallowing per subscriber-isolation contract.",
                shell.Descriptor.Name, previous, current);
        }

        return Task.CompletedTask;
    }
}
