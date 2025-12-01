using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace CShells.AspNetCore.Routing;

/// <summary>
/// Provides a dynamic collection of endpoints for shell-based routing.
/// Supports adding and removing shells at runtime, triggering endpoint re-evaluation.
/// </summary>
public class DynamicShellEndpointDataSource : EndpointDataSource
{
    private readonly List<Endpoint> _endpoints = [];
    private readonly Lock _lock = new();
    private CancellationTokenSource _cts = new();

    /// <summary>
    /// Gets the current collection of endpoints.
    /// </summary>
    public override IReadOnlyList<Endpoint> Endpoints
    {
        get
        {
            lock (_lock)
            {
                return _endpoints.ToList();
            }
        }
    }

    /// <summary>
    /// Returns a change token that signals when endpoints have been modified.
    /// </summary>
    public override IChangeToken GetChangeToken()
    {
        return new CancellationChangeToken(_cts.Token);
    }

    /// <summary>
    /// Adds endpoints for a shell.
    /// </summary>
    /// <param name="endpoints">The endpoints to add.</param>
    public void AddEndpoints(IEnumerable<Endpoint> endpoints)
    {
        lock (_lock)
        {
            _endpoints.AddRange(endpoints);
            NotifyChanged();
        }
    }

    /// <summary>
    /// Removes all endpoints for a specific shell.
    /// </summary>
    /// <param name="shellId">The shell ID whose endpoints should be removed.</param>
    public void RemoveEndpoints(ShellId shellId)
    {
        lock (_lock)
        {
            // Remove endpoints that belong to this shell
            _endpoints.RemoveAll(e =>
                e.Metadata.GetMetadata<ShellEndpointMetadata>()?.ShellId.Equals(shellId) ?? false);
            NotifyChanged();
        }
    }

    /// <summary>
    /// Clears all endpoints.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _endpoints.Clear();
            NotifyChanged();
        }
    }

    /// <summary>
    /// Notifies the routing system that endpoints have changed.
    /// </summary>
    private void NotifyChanged()
    {
        var oldCts = _cts;
        _cts = new CancellationTokenSource();
        oldCts.Cancel();
        oldCts.Dispose();
    }
}
