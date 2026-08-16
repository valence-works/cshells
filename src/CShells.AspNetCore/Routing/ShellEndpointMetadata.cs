namespace CShells.AspNetCore.Routing;

/// <summary>
/// Identifies the conceptual owner of an ASP.NET Core endpoint.
/// </summary>
public enum EndpointOwnerKind
{
    /// <summary>The host application owns the endpoint.</summary>
    Host,

    /// <summary>A statically composed module owns the endpoint.</summary>
    Module,

    /// <summary>A dynamically loaded shell feature owns the endpoint.</summary>
    DynamicShell,
}

/// <summary>
/// Typed route ownership metadata used by endpoint inventory and collision diagnostics.
/// </summary>
/// <param name="OwnerKind">The conceptual owner category.</param>
/// <param name="OwnerId">The stable owner identifier.</param>
/// <param name="ShellId">The shell identifier for dynamic-shell endpoints.</param>
/// <param name="Generation">The shell generation for dynamic-shell endpoints.</param>
public sealed record EndpointOwnershipMetadata(
    EndpointOwnerKind OwnerKind,
    string OwnerId,
    ShellId? ShellId = null,
    int? Generation = null);

/// <summary>
/// Metadata attached to endpoints to identify which shell they belong to.
/// </summary>
/// <param name="ShellId">The ID of the shell that owns this endpoint.</param>
/// <param name="Generation">The shell generation that registered this endpoint.</param>
/// <param name="ShellSettings">The shell settings for this endpoint.</param>
/// <param name="FeatureName">The feature that mapped the endpoint, when known.</param>
public record ShellEndpointMetadata(
    ShellId ShellId,
    int Generation,
    ShellSettings ShellSettings,
    string? FeatureName = null)
{
    /// <summary>Gets the conceptual owner kind for this endpoint.</summary>
    public EndpointOwnerKind OwnerKind => EndpointOwnerKind.DynamicShell;

    /// <summary>Gets the stable feature or shell owner identifier.</summary>
    public string OwnerId => string.IsNullOrWhiteSpace(FeatureName) ? ShellId.Name : FeatureName;
}

/// <summary>
/// Describes a deterministic conflict between two route endpoints.
/// </summary>
/// <param name="CandidateOwner">Structured ownership of the unpublished candidate endpoint.</param>
/// <param name="ExistingOwner">Structured ownership of the already published endpoint.</param>
/// <param name="CandidateMethods">Methods accepted by the candidate endpoint.</param>
/// <param name="ExistingMethods">Methods accepted by the existing endpoint.</param>
/// <param name="CandidatePattern">The candidate route template.</param>
/// <param name="ExistingPattern">The existing route template.</param>
public sealed record ShellEndpointConflict(
    EndpointOwnershipMetadata CandidateOwner,
    EndpointOwnershipMetadata ExistingOwner,
    IReadOnlyList<string> CandidateMethods,
    IReadOnlyList<string> ExistingMethods,
    string CandidatePattern,
    string ExistingPattern);

/// <summary>
/// Thrown when an endpoint generation contains a route collision with an existing route or
/// another endpoint in the same candidate batch.
/// </summary>
public sealed class ShellEndpointConflictException : InvalidOperationException
{
    /// <summary>Initializes a new exception with structured conflict details.</summary>
    /// <param name="conflict">The structured candidate and existing endpoint conflict.</param>
    public ShellEndpointConflictException(ShellEndpointConflict conflict)
        : base(CreateMessage(Guard.Against.Null(conflict)))
    {
        Conflict = conflict;
    }

    /// <summary>Gets the structured route conflict details.</summary>
    public ShellEndpointConflict Conflict { get; }

    private static string CreateMessage(ShellEndpointConflict conflict) =>
        $"Endpoint route conflict: candidate owner '{FormatOwner(conflict.CandidateOwner)}' " +
        $"({string.Join(", ", conflict.CandidateMethods)} {conflict.CandidatePattern}) " +
        $"conflicts with existing owner '{FormatOwner(conflict.ExistingOwner)}' " +
        $"({string.Join(", ", conflict.ExistingMethods)} {conflict.ExistingPattern}).";

    private static string FormatOwner(EndpointOwnershipMetadata owner)
    {
        var description = $"{owner.OwnerKind}:{owner.OwnerId}";
        return owner.ShellId is { } shellId && owner.Generation is { } generation
            ? $"{description}, shell '{shellId}' generation {generation}"
            : description;
    }
}
