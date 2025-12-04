namespace CShells.Workbench.Features.Core;

/// <summary>
/// Default implementation of tenant information.
/// </summary>
public class TenantInfo : ITenantInfo
{
    public required string TenantId { get; init; }
    public required string TenantName { get; init; }
    public required string Tier { get; init; }
}
