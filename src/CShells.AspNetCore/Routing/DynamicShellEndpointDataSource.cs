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
            var retained = published.Where(endpoint =>
            {
                var metadata = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
                return metadata is null || !metadata.ShellId.Equals(shellId);
            });

            Volatile.Write(ref _publishedEndpoints, [..retained, ..candidate]);
            NotifyChanged();
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
            var published = Volatile.Read(ref _publishedEndpoints);
            var retained = published.Where(endpoint =>
            {
                var metadata = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
                return metadata is null || !metadata.ShellId.Equals(shellId) || metadata.Generation != generation;
            }).ToArray();
            if (retained.Length != published.Count)
            {
                Volatile.Write(ref _publishedEndpoints, retained);
                NotifyChanged();
            }
        }
    }

    /// <summary>
    /// Clears all endpoints.
    /// </summary>
    public void Clear()
    {
        lock (_publicationLock)
        {
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

    private static bool MethodsConflict(IReadOnlyCollection<string> candidate, IReadOnlyCollection<string> existing) =>
        candidate.Contains("*", StringComparer.OrdinalIgnoreCase)
        || existing.Contains("*", StringComparer.OrdinalIgnoreCase)
        || candidate.Any(existing.Contains);

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

    private static string DescribeOwner(Endpoint endpoint)
    {
        var ownership = endpoint.Metadata.GetMetadata<EndpointOwnershipMetadata>();
        if (ownership is not null)
            return $"{ownership.OwnerKind}:{ownership.OwnerId}";

        var shell = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
        if (shell is not null)
            return $"{shell.OwnerKind}:{shell.OwnerId}";

        return $"Host:{endpoint.DisplayName ?? "(unnamed endpoint)"}";
    }

    private static bool IsHostEndpoint(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<ShellEndpointMetadata>() is null;

    private void NotifyChanged()
    {
        var oldCts = _cts;
        _cts = new();
        oldCts.Cancel();
        oldCts.Dispose();
    }
}
