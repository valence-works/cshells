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
    private readonly Dictionary<ShellId, long> _shellVersions = [];
    // Route collision validity is global: a rollback can restore routes that overlap a newer
    // shell or host mutation. Keep exactly one rollback-capable commit until its owner finalizes.
    private PendingTransaction? _pendingTransaction;
    private long _nextTransactionId;
    private CancellationTokenSource _cts = new();
    private readonly ILogger<DynamicShellEndpointDataSource> _logger = logger ?? NullLogger<DynamicShellEndpointDataSource>.Instance;

    /// <inheritdoc />
    public override IReadOnlyList<Endpoint> Endpoints
    {
        get => Volatile.Read(ref _publishedEndpoints);
    }

    /// <inheritdoc />
    public override IChangeToken GetChangeToken() =>
        new CancellationChangeToken(Volatile.Read(ref _cts).Token);

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
            EnsureNoPendingTransaction("add endpoints");

            ValidateCandidate(additions, existingShellId: null, allowSameShellGenerations: true);
            var published = Volatile.Read(ref _publishedEndpoints);
            Volatile.Write(ref _publishedEndpoints, [.. published, .. additions]);
            AdvanceVersions(additions);
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
        using var publication = PrepareGeneration(shellId, generation, endpoints, hostEndpoints);
        publication.Commit();
        publication.Complete();
    }

    /// <summary>
    /// Prepares an endpoint generation without changing the routing-visible snapshot. Preparation
    /// may overlap; commit revalidates under the publication lock and is rejected while another
    /// rollback-capable commit owns the global route inventory.
    /// </summary>
    internal ShellEndpointGenerationPublication PrepareGeneration(
        ShellId shellId,
        int generation,
        IEnumerable<Endpoint> endpoints,
        IEnumerable<Endpoint>? hostEndpoints = null)
    {
        Guard.Against.Null(endpoints);
        var candidate = endpoints.ToArray();
        var candidateHostEndpoints = hostEndpoints?.Where(IsHostEndpoint).ToArray();

        lock (_publicationLock)
        {
            if (candidateHostEndpoints is not null)
            {
                EnsureNoPendingTransaction("replace the host endpoint inventory");
                _hostEndpoints = candidateHostEndpoints;
            }

            ValidateGenerationIdentity(shellId, generation, candidate);
            ValidateCandidate(
                candidate,
                shellId,
                allowSameShellGenerations: false,
                Volatile.Read(ref _hostEndpoints));
        }

        return new ShellEndpointGenerationPublication(
            this,
            Interlocked.Increment(ref _nextTransactionId),
            shellId,
            generation,
            candidate);
    }

    /// <summary>Retires a generation during normal deactivation.</summary>
    internal void RetireGeneration(ShellId shellId, int generation)
    {
        lock (_publicationLock)
        {
            if (DeferPendingGenerationRemoval(shellId, generation))
                return;

            RemoveGeneration(shellId, generation, Volatile.Read(ref _publishedEndpoints));
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
        {
            EnsureNoPendingTransaction("replace the host endpoint inventory");
            _hostEndpoints = endpoints.Where(IsHostEndpoint).ToArray();
        }
    }

    /// <summary>
    /// Removes all endpoints for a specific shell.
    /// </summary>
    public void RemoveEndpoints(ShellId shellId)
    {
        lock (_publicationLock)
        {
            EnsurePendingTransactionDoesNotOwn(shellId, "remove every generation");
            var published = Volatile.Read(ref _publishedEndpoints);
            var retained = published.Where(endpoint =>
            {
                var metadata = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
                return metadata is null || !metadata.ShellId.Equals(shellId);
            }).ToArray();
            if (retained.Length != published.Count)
            {
                Volatile.Write(ref _publishedEndpoints, retained);
                AdvanceVersion(shellId);
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
            if (DeferPendingGenerationRemoval(shellId, generation))
                return;

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
            EnsureNoPendingTransaction("clear endpoints");

            var published = Volatile.Read(ref _publishedEndpoints);
            if (published.Count == 0)
                return;

            Volatile.Write(ref _publishedEndpoints, []);
            AdvanceVersions(published);
            NotifyChanged();
        }
    }

    private void ValidateCandidate(
        IReadOnlyList<Endpoint> candidate,
        ShellId? existingShellId,
        bool allowSameShellGenerations,
        IReadOnlyList<Endpoint>? hostEndpoints = null)
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
            .Concat(hostEndpoints ?? Volatile.Read(ref _hostEndpoints))
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

    private CommittedGeneration CommitGeneration(
        long transactionId,
        ShellId shellId,
        int generation,
        IReadOnlyList<Endpoint> candidate)
    {
        lock (_publicationLock)
        {
            EnsureNoPendingTransaction("commit another generation");

            // Preparation can be arbitrarily delayed. Revalidate against the latest endpoint
            // inventory at commit so concurrent publications are serialized deterministically.
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

            _pendingTransaction = new PendingTransaction(transactionId, shellId);
            Volatile.Write(ref _publishedEndpoints, [.. retained, .. candidate]);
            var version = AdvanceVersion(shellId);
            NotifyChanged();
            return new CommittedGeneration(version, retired);
        }
    }

    private void RollbackGeneration(
        long transactionId,
        ShellId shellId,
        long committedVersion,
        IReadOnlyList<Endpoint> retired)
    {
        lock (_publicationLock)
        {
            if (_pendingTransaction is not { } pending
                || pending.TransactionId != transactionId
                || !pending.ShellId.Equals(shellId))
                return;

            if (GetVersion(shellId) != committedVersion)
            {
                throw new InvalidOperationException(
                    $"Shell endpoint publication transaction {transactionId} for '{shellId}' lost ownership of its committed snapshot.");
            }

            var published = Volatile.Read(ref _publishedEndpoints);
            var retained = published.Where(endpoint =>
            {
                var metadata = endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
                return metadata is null || !metadata.ShellId.Equals(shellId);
            });
            Volatile.Write(ref _publishedEndpoints, [.. retained, .. retired]);
            AdvanceVersion(shellId);
            NotifyChanged();
            ApplyDeferredGenerationRemovals(pending);
            _pendingTransaction = null;
        }
    }

    private void CompleteGeneration(long transactionId, ShellId shellId)
    {
        lock (_publicationLock)
        {
            if (_pendingTransaction is not { } pending
                || pending.TransactionId != transactionId
                || !pending.ShellId.Equals(shellId))
            {
                throw new InvalidOperationException(
                    $"Shell endpoint publication transaction {transactionId} for '{shellId}' is not the pending committed transaction.");
            }

            ApplyDeferredGenerationRemovals(pending);
            _pendingTransaction = null;
        }
    }

    private bool DeferPendingGenerationRemoval(ShellId shellId, int generation)
    {
        if (_pendingTransaction is not { } pending || !pending.ShellId.Equals(shellId))
            return false;

        pending.DeferredGenerationRemovals.Add(generation);
        return true;
    }

    private void ApplyDeferredGenerationRemovals(PendingTransaction pending)
    {
        foreach (var generation in pending.DeferredGenerationRemovals)
            RemoveGeneration(pending.ShellId, generation, Volatile.Read(ref _publishedEndpoints));
    }

    private void EnsurePendingTransactionDoesNotOwn(ShellId shellId, string operation)
    {
        if (_pendingTransaction is { } pending && pending.ShellId.Equals(shellId))
        {
            throw new InvalidOperationException(
                $"Cannot {operation} for shell '{shellId}' while endpoint publication transaction " +
                $"{pending.TransactionId} owns its rollback snapshot.");
        }
    }

    private void EnsureNoPendingTransaction(string operation)
    {
        if (_pendingTransaction is { } pending)
        {
            throw new InvalidOperationException(
                $"Cannot {operation} while endpoint publication transaction {pending.TransactionId} " +
                $"for shell '{pending.ShellId}' is awaiting completion or rollback.");
        }
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
        AdvanceVersion(shellId);
        NotifyChanged();
    }

    private long AdvanceVersion(ShellId shellId)
    {
        var version = GetVersion(shellId) + 1;
        _shellVersions[shellId] = version;
        return version;
    }

    private void AdvanceVersions(IEnumerable<Endpoint> endpoints)
    {
        foreach (var shellId in endpoints
                     .Select(endpoint => endpoint.Metadata.GetMetadata<ShellEndpointMetadata>()?.ShellId)
                     .OfType<ShellId>()
                     .Distinct())
            AdvanceVersion(shellId);
    }

    private long GetVersion(ShellId shellId) =>
        _shellVersions.GetValueOrDefault(shellId);

    private void NotifyChanged()
    {
        var previous = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        try
        {
            previous.Cancel();
        }
        catch (AggregateException ex)
        {
            // Change observers must not be able to turn an already-visible atomic publication
            // into a failed commit. Routing will observe the new snapshot on its next read.
            _logger.LogError(ex, "An endpoint change observer threw while processing publication.");
        }
    }

    internal sealed class ShellEndpointGenerationPublication : IDisposable
    {
        private readonly DynamicShellEndpointDataSource owner;
        private readonly long transactionId;
        private readonly ShellId shellId;
        private readonly int generation;
        private readonly object gate = new();
        private IReadOnlyList<Endpoint> candidate;
        private IReadOnlyList<Endpoint> retired = [];
        private long committedVersion;
        private PublicationState state;

        internal ShellEndpointGenerationPublication(
            DynamicShellEndpointDataSource owner,
            long transactionId,
            ShellId shellId,
            int generation,
            IReadOnlyList<Endpoint> candidate)
        {
            this.owner = owner;
            this.transactionId = transactionId;
            this.shellId = shellId;
            this.generation = generation;
            this.candidate = candidate;
        }

        internal void Commit()
        {
            lock (gate)
            {
                if (state != PublicationState.Prepared)
                    throw new InvalidOperationException($"Endpoint generation publication is already {state}.");

                var committed = owner.CommitGeneration(transactionId, shellId, generation, candidate);
                retired = committed.Retired;
                committedVersion = committed.Version;
                state = PublicationState.Committed;
                ReleaseCandidate();
            }
        }

        internal void Complete()
        {
            lock (gate)
            {
                if (state != PublicationState.Committed)
                    throw new InvalidOperationException($"Endpoint generation publication is already {state}.");

                owner.CompleteGeneration(transactionId, shellId);
                state = PublicationState.Completed;
                ReleaseRollback();
            }
        }

        internal void Rollback()
        {
            lock (gate)
            {
                if (state == PublicationState.Prepared)
                {
                    state = PublicationState.RolledBack;
                    ReleaseCandidate();
                    return;
                }

                if (state != PublicationState.Committed)
                    return;

                owner.RollbackGeneration(transactionId, shellId, committedVersion, retired);
                state = PublicationState.RolledBack;
                ReleaseRollback();
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (state == PublicationState.Prepared)
                    Rollback();
                else if (state == PublicationState.Committed)
                    Complete();
            }
        }

        private void ReleaseCandidate()
        {
            candidate = [];
        }

        private void ReleaseRollback()
        {
            retired = [];
            committedVersion = 0;
        }

        private enum PublicationState
        {
            Prepared,
            Committed,
            Completed,
            RolledBack,
        }
    }

    private sealed record CommittedGeneration(long Version, IReadOnlyList<Endpoint> Retired);
    private sealed class PendingTransaction(long transactionId, ShellId shellId)
    {
        internal long TransactionId { get; } = transactionId;
        internal ShellId ShellId { get; } = shellId;
        internal HashSet<int> DeferredGenerationRemovals { get; } = [];
    }
}
