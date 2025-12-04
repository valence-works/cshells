namespace CShells.Workbench.Features.Core;

/// <summary>
/// Provides information about the current tenant.
/// </summary>
public interface ITenantInfo
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    string TenantId { get; }

    /// <summary>
    /// Gets the tenant's display name.
    /// </summary>
    string TenantName { get; }

    /// <summary>
    /// Gets the tenant's subscription tier.
    /// </summary>
    string Tier { get; }
}
