namespace CShells.Workbench.Features.Core;

/// <summary>
/// Simple console audit logger implementation.
/// </summary>
public class AuditLogger(ITenantInfo tenantInfo) : IAuditLogger
{
    public void LogInfo(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:u}] [{tenantInfo.TenantId}] {message}");
    }
}
