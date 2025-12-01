using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly ILogger<DynamicShellEndpointDataSource> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicShellEndpointDataSource"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public DynamicShellEndpointDataSource(ILogger<DynamicShellEndpointDataSource>? logger = null)
    {
        _logger = logger ?? NullLogger<DynamicShellEndpointDataSource>.Instance;
    }

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
            var newEndpoints = endpoints.ToList();

            // Check for potential conflicts with existing endpoints
            DetectPathConflicts(newEndpoints);

            _endpoints.AddRange(newEndpoints);
            NotifyChanged();
        }
    }

    /// <summary>
    /// Detects potential path conflicts between new endpoints and existing endpoints.
    /// Logs warnings when conflicts are detected.
    /// </summary>
    /// <param name="newEndpoints">The endpoints being added.</param>
    private void DetectPathConflicts(List<Endpoint> newEndpoints)
    {
        foreach (var newEndpoint in newEndpoints.OfType<RouteEndpoint>())
        {
            var newPattern = newEndpoint.RoutePattern.RawText ?? "";
            var newMethod = newEndpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.FirstOrDefault() ?? "ANY";
            var newShellMetadata = newEndpoint.Metadata.GetMetadata<ShellEndpointMetadata>();

            foreach (var existingEndpoint in _endpoints.OfType<RouteEndpoint>())
            {
                var existingPattern = existingEndpoint.RoutePattern.RawText ?? "";
                var existingMethod = existingEndpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.FirstOrDefault() ?? "ANY";
                var existingShellMetadata = existingEndpoint.Metadata.GetMetadata<ShellEndpointMetadata>();

                // Check if patterns match and methods overlap
                if (PatternsConflict(newPattern, existingPattern) && MethodsConflict(newMethod, existingMethod))
                {
                    if (newShellMetadata != null && existingShellMetadata != null)
                    {
                        _logger.LogWarning(
                            "Path conflict detected: Shell '{NewShell}' endpoint '{NewMethod} {NewPattern}' conflicts with shell '{ExistingShell}' endpoint '{ExistingMethod} {ExistingPattern}'. " +
                            "This may cause routing ambiguity.",
                            newShellMetadata.ShellId, newMethod, newPattern,
                            existingShellMetadata.ShellId, existingMethod, existingPattern);
                    }
                    else if (newShellMetadata != null)
                    {
                        _logger.LogWarning(
                            "Path conflict detected: Shell '{NewShell}' endpoint '{NewMethod} {NewPattern}' conflicts with host application endpoint '{ExistingMethod} {ExistingPattern}'. " +
                            "Shell routes may override host routes.",
                            newShellMetadata.ShellId, newMethod, newPattern, existingMethod, existingPattern);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Determines if two route patterns conflict (could match the same request).
    /// </summary>
    private static bool PatternsConflict(string pattern1, string pattern2)
    {
        // Exact match is definitely a conflict
        if (string.Equals(pattern1, pattern2, StringComparison.OrdinalIgnoreCase))
            return true;

        // Both empty/root patterns conflict
        if (string.IsNullOrEmpty(pattern1) && string.IsNullOrEmpty(pattern2))
            return true;

        // For now, use simple exact matching
        // A more sophisticated implementation would parse route templates and check parameter patterns
        return false;
    }

    /// <summary>
    /// Determines if two HTTP methods conflict.
    /// </summary>
    private static bool MethodsConflict(string method1, string method2)
    {
        // ANY matches everything
        if (method1 == "ANY" || method2 == "ANY")
            return true;

        // Same method conflicts
        return string.Equals(method1, method2, StringComparison.OrdinalIgnoreCase);
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
