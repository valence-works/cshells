using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
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
    private IReadOnlyList<Endpoint> _publishedEndpoints = [];
    private IReadOnlyList<Endpoint> _hostEndpoints = [];
    private readonly Dictionary<ShellId, PendingGenerationReplacement> _pendingReplacements = [];
    private readonly object _publicationLock = new();
    private CancellationTokenSource _cts = new();
    private readonly ILogger<DynamicShellEndpointDataSource> _logger = logger ?? NullLogger<DynamicShellEndpointDataSource>.Instance;

    /// <inheritdoc />
    public override IReadOnlyList<Endpoint> Endpoints
    {
        get => Volatile.Read(ref _publishedEndpoints);
    }

    /// <inheritdoc />
    public override IChangeToken GetChangeToken() => new CancellationChangeToken(_cts.Token);

    /// <summary>
    /// Adds endpoints for a shell.
    /// </summary>
    public void AddEndpoints(IEnumerable<Endpoint> endpoints)
    {
        Guard.Against.Null(endpoints);
        var additions = endpoints.ToArray();
        if (additions.Length == 0)
            return;

        lock (_publicationLock)
        {
            ValidateCandidate(additions, existingShellId: null, allowSameShellGenerations: true);
            var published = Volatile.Read(ref _publishedEndpoints);
            Volatile.Write(ref _publishedEndpoints, [..published, ..additions]);
            NotifyChanged();
        }
    }

    /// <summary>
    /// Replaces every published generation for a shell with one validated candidate snapshot.
    /// Validation completes before the snapshot changes, so a rejected candidate leaves the
    /// previous generation available and a successful replacement never publishes an empty state.
    /// </summary>
    /// <param name="shellId">The shell whose generation is being replaced.</param>
    /// <param name="generation">The candidate generation number.</param>
    /// <param name="endpoints">The complete mapped endpoint candidate.</param>
    /// <param name="hostEndpoints">Optional current host endpoint snapshot for collision checks.</param>
    public void PublishGeneration(
        ShellId shellId,
        int generation,
        IEnumerable<Endpoint> endpoints,
        IEnumerable<Endpoint>? hostEndpoints = null)
    {
        Guard.Against.Null(endpoints);
        var candidate = endpoints.ToArray();

        lock (_publicationLock)
        {
            if (hostEndpoints is not null)
                _hostEndpoints = hostEndpoints.Where(IsHostEndpoint).ToArray();

            ValidateGenerationIdentity(shellId, generation, candidate);
            ValidateCandidate(candidate, shellId, allowSameShellGenerations: false);

            var published = Volatile.Read(ref _publishedEndpoints);
            var retired = published.Where(endpoint =>
            {
                var metadata = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
                return metadata is not null && metadata.ShellId.Equals(shellId);
            }).ToArray();
            var retained = published.Where(endpoint =>
            {
                var metadata = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
                return metadata is null || !metadata.ShellId.Equals(shellId);
            });

            _pendingReplacements[shellId] = new PendingGenerationReplacement(generation, retired);
            Volatile.Write(ref _publishedEndpoints, [..retained, ..candidate]);
            NotifyChanged();
        }
    }

    /// <summary>
    /// Retires a generation during normal deactivation. This commits any pending replacement
    /// transaction involving that generation and removes its published endpoints.
    /// </summary>
    internal void RetireGeneration(ShellId shellId, int generation)
    {
        lock (_publicationLock)
        {
            CommitPendingReplacement(shellId, generation);
            RemoveGeneration(shellId, generation, Volatile.Read(ref _publishedEndpoints));
        }
    }

    /// <summary>
    /// Rejects a candidate generation. When the generation still owns a pending replacement,
    /// its retired endpoint snapshot is restored atomically; otherwise only that generation is removed.
    /// </summary>
    internal void RollbackGeneration(ShellId shellId, int generation)
    {
        lock (_publicationLock)
        {
            var published = Volatile.Read(ref _publishedEndpoints);
            if (_pendingReplacements.TryGetValue(shellId, out var pending) && pending.CandidateGeneration == generation)
            {
                var retained = published.Where(endpoint =>
                {
                    var metadata = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
                    return metadata is null || !metadata.ShellId.Equals(shellId);
                });
                var changed = pending.RetiredEndpoints.Count > 0 || published.Any(endpoint =>
                    endpoint.Metadata.GetMetadata<ShellEndpointMetadata>()?.ShellId.Equals(shellId) == true);

                _pendingReplacements.Remove(shellId);
                Volatile.Write(ref _publishedEndpoints, [..retained, ..pending.RetiredEndpoints]);
                if (changed)
                    NotifyChanged();
                return;
            }

            RemoveGeneration(shellId, generation, published);
        }
    }

    /// <summary>
    /// Updates the host endpoint snapshot used by candidate collision validation.
    /// Shell-owned endpoints are ignored automatically.
    /// </summary>
    /// <param name="endpoints">The standard ASP.NET Core host endpoint inventory.</param>
    public void SetHostEndpoints(IEnumerable<Endpoint> endpoints)
    {
        Guard.Against.Null(endpoints);
        lock (_publicationLock)
            _hostEndpoints = endpoints.Where(IsHostEndpoint).ToArray();
    }

    /// <summary>
    /// Removes all endpoints for a specific shell.
    /// </summary>
    public void RemoveEndpoints(ShellId shellId)
    {
        lock (_publicationLock)
        {
            _pendingReplacements.Remove(shellId);
            var published = Volatile.Read(ref _publishedEndpoints);
            var retained = published.Where(endpoint =>
            {
                var metadata = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
                return metadata is null || !metadata.ShellId.Equals(shellId);
            }).ToArray();
            if (retained.Length != published.Count)
            {
                Volatile.Write(ref _publishedEndpoints, retained);
                NotifyChanged();
            }
        }
    }

    /// <summary>
    /// Removes endpoints for a specific shell generation only, leaving newer generations intact.
    /// </summary>
    public void RemoveEndpoints(ShellId shellId, int generation)
    {
        lock (_publicationLock)
        {
            CommitPendingReplacement(shellId, generation);
            var published = Volatile.Read(ref _publishedEndpoints);
            RemoveGeneration(shellId, generation, published);
        }
    }

    /// <summary>
    /// Clears all endpoints.
    /// </summary>
    public void Clear()
    {
        lock (_publicationLock)
        {
            _pendingReplacements.Clear();
            var published = Volatile.Read(ref _publishedEndpoints);
            if (published.Count == 0)
                return;

            Volatile.Write(ref _publishedEndpoints, []);
            NotifyChanged();
        }
    }

    private void ValidateCandidate(
        IReadOnlyList<Endpoint> candidate,
        ShellId? existingShellId,
        bool allowSameShellGenerations)
    {
        var published = Volatile.Read(ref _publishedEndpoints);
        var candidateShellIds = candidate
            .Select(endpoint => endpoint.Metadata.GetMetadata<ShellEndpointMetadata>()?.ShellId)
            .OfType<ShellId>()
            .ToHashSet();
        var candidateGenerations = candidate
            .Select(endpoint => endpoint.Metadata.GetMetadata<ShellEndpointMetadata>())
            .Where(metadata => metadata is not null)
            .GroupBy(metadata => metadata!.ShellId)
            .ToDictionary(group => group.Key, group => group.Select(metadata => metadata!.Generation).ToHashSet());
        var existing = published
            .Concat(Volatile.Read(ref _hostEndpoints))
            .Where(endpoint =>
            {
                if (existingShellId is null && allowSameShellGenerations)
                {
                    var existingMetadata = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
                    return existingMetadata is null
                           || !candidateShellIds.Contains(existingMetadata.ShellId)
                           || candidateGenerations[existingMetadata.ShellId].Contains(existingMetadata.Generation);
                }

                if (existingShellId is null)
                    return true;

                var metadata = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
                return metadata is null || !metadata.ShellId.Equals(existingShellId);
            })
            .OfType<RouteEndpoint>()
            .ToArray();

        var candidateRoutes = candidate.OfType<RouteEndpoint>().ToArray();
        for (var i = 0; i < candidateRoutes.Length; i++)
        {
            var candidateEndpoint = candidateRoutes[i];
            foreach (var existingEndpoint in existing.Concat(candidateRoutes.Take(i)))
            {
                if (!RoutesConflict(candidateEndpoint, existingEndpoint))
                    continue;

                var conflict = BuildConflict(candidateEndpoint, existingEndpoint);
                _logger.LogError("{Message}", new ShellEndpointConflictException(conflict).Message);
                throw new ShellEndpointConflictException(conflict);
            }
        }
    }

    private static void ValidateGenerationIdentity(ShellId shellId, int generation, IEnumerable<Endpoint> candidate)
    {
        foreach (var endpoint in candidate)
        {
            var metadata = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
            if (metadata is null)
                continue;

            if (!metadata.ShellId.Equals(shellId) || metadata.Generation != generation)
            {
                throw new InvalidOperationException(
                    $"Endpoint candidate metadata identifies shell '{metadata.ShellId}' generation {metadata.Generation}, " +
                    $"but publication targets shell '{shellId}' generation {generation}.");
            }
        }
    }

    private static bool RoutesConflict(RouteEndpoint candidate, RouteEndpoint existing) =>
        string.Equals(NormalizePattern(candidate.RoutePattern), NormalizePattern(existing.RoutePattern), StringComparison.OrdinalIgnoreCase)
        && MethodsConflict(GetMethods(candidate), GetMethods(existing));

    internal static bool MethodsConflict(IReadOnlyCollection<string> candidate, IReadOnlyCollection<string> existing) =>
        candidate.Contains("*", StringComparer.OrdinalIgnoreCase)
        || existing.Contains("*", StringComparer.OrdinalIgnoreCase)
        || candidate.Any(method => existing.Contains(method, StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<string> GetMethods(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        return methods is null || methods.Count == 0 ? ["*"] : methods.OrderBy(method => method, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string NormalizePattern(RoutePattern pattern)
    {
        var segments = pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(NormalizePart)));
        return "/" + string.Join("/", segments);
    }

    private static string NormalizePart(RoutePatternPart part) => part switch
    {
        RoutePatternLiteralPart literal => literal.Content.ToLowerInvariant(),
        RoutePatternParameterPart parameter => "{" +
                                               (parameter.IsCatchAll ? "**" : parameter.IsOptional ? "?" : "") +
                                               string.Join(",", parameter.ParameterPolicies.Select(policy => policy.Content?.ToLowerInvariant() ?? string.Empty)) +
                                               "}",
        _ => part.ToString()?.ToLowerInvariant() ?? string.Empty,
    };

    private static ShellEndpointConflict BuildConflict(RouteEndpoint candidate, RouteEndpoint existing)
    {
        var candidateOwner = DescribeOwner(candidate);
        var existingOwner = DescribeOwner(existing);
        return new ShellEndpointConflict(
            candidateOwner,
            existingOwner,
            GetMethods(candidate),
            GetMethods(existing),
            candidate.RoutePattern.RawText ?? string.Empty,
            existing.RoutePattern.RawText ?? string.Empty);
    }

    private static EndpointOwnershipMetadata DescribeOwner(Endpoint endpoint)
    {
        var ownership = endpoint.Metadata.GetMetadata<EndpointOwnershipMetadata>();
        if (ownership is not null)
            return ownership;

        var shell = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
        if (shell is not null)
            return new EndpointOwnershipMetadata(shell.OwnerKind, shell.OwnerId, shell.ShellId, shell.Generation);

        return new EndpointOwnershipMetadata(
            EndpointOwnerKind.Host,
            endpoint.DisplayName ?? "(unnamed endpoint)");
    }

    private static bool IsHostEndpoint(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<ShellEndpointMetadata>() is null;

    private void CommitPendingReplacement(ShellId shellId, int generation)
    {
        if (!_pendingReplacements.TryGetValue(shellId, out var pending))
            return;

        var retiresGeneration = pending.RetiredEndpoints.Any(endpoint =>
            endpoint.Metadata.GetMetadata<ShellEndpointMetadata>()?.Generation == generation);
        if (pending.CandidateGeneration == generation || retiresGeneration)
            _pendingReplacements.Remove(shellId);
    }

    private void RemoveGeneration(ShellId shellId, int generation, IReadOnlyList<Endpoint> published)
    {
        var retained = published.Where(endpoint =>
        {
            var metadata = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
            return metadata is null || !metadata.ShellId.Equals(shellId) || metadata.Generation != generation;
        }).ToArray();
        if (retained.Length == published.Count)
            return;

        Volatile.Write(ref _publishedEndpoints, retained);
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        var oldCts = _cts;
        _cts = new();
        oldCts.Cancel();
        oldCts.Dispose();
    }

    private sealed record PendingGenerationReplacement(
        int CandidateGeneration,
        IReadOnlyList<Endpoint> RetiredEndpoints);
}
