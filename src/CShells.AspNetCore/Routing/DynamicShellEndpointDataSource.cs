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
public class DynamicShellEndpointDataSource(ILogger<DynamicShellEndpointDataSource>? logger = null) : EndpointDataSource
{
    private readonly List<Endpoint> _endpoints = [];
    private readonly Lock _lock = new();
    private CancellationTokenSource _cts = new();
    private readonly ILogger<DynamicShellEndpointDataSource> _logger = logger ?? NullLogger<DynamicShellEndpointDataSource>.Instance;

    /// <inheritdoc />
    public override IReadOnlyList<Endpoint> Endpoints
    {
        get
        {
            lock (_lock)
                return [.._endpoints];
        }
    }

    /// <inheritdoc />
    public override IChangeToken GetChangeToken() => new CancellationChangeToken(_cts.Token);

    /// <summary>
    /// Adds endpoints for a shell.
    /// </summary>
    public void AddEndpoints(IEnumerable<Endpoint> endpoints)
    {
        lock (_lock)
        {
            var newEndpoints = endpoints.ToList();
            DetectPathConflicts(newEndpoints);
            _endpoints.AddRange(newEndpoints);
            NotifyChanged();
        }
    }

    /// <summary>
    /// Removes all endpoints for a specific shell.
    /// </summary>
    public void RemoveEndpoints(ShellId shellId)
    {
        lock (_lock)
        {
            _endpoints.RemoveAll(e => e.Metadata.GetMetadata<ShellEndpointMetadata>()?.ShellId.Equals(shellId) ?? false);
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

                if (!PatternsConflict(newPattern, existingPattern) || !MethodsConflict(newMethod, existingMethod))
                    continue;

                if (newShellMetadata != null && existingShellMetadata != null)
                {
                    _logger.LogWarning(
                        "Path conflict detected: Shell '{NewShell}' endpoint '{NewMethod} {NewPattern}' conflicts with shell '{ExistingShell}' endpoint '{ExistingMethod} {ExistingPattern}'.",
                        newShellMetadata.ShellId, newMethod, newPattern,
                        existingShellMetadata.ShellId, existingMethod, existingPattern);
                }
                else if (newShellMetadata != null)
                {
                    _logger.LogWarning(
                        "Path conflict detected: Shell '{NewShell}' endpoint '{NewMethod} {NewPattern}' conflicts with host application endpoint '{ExistingMethod} {ExistingPattern}'.",
                        newShellMetadata.ShellId, newMethod, newPattern, existingMethod, existingPattern);
                }
            }
        }
    }

    private static bool PatternsConflict(string pattern1, string pattern2) =>
        string.Equals(pattern1, pattern2, StringComparison.OrdinalIgnoreCase) ||
        (string.IsNullOrEmpty(pattern1) && string.IsNullOrEmpty(pattern2));

    private static bool MethodsConflict(string method1, string method2) =>
        method1 == "ANY" || method2 == "ANY" || string.Equals(method1, method2, StringComparison.OrdinalIgnoreCase);

    private void NotifyChanged()
    {
        var oldCts = _cts;
        _cts = new();
        oldCts.Cancel();
        oldCts.Dispose();
    }
}
