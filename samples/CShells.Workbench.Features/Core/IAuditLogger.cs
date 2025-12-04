namespace CShells.Workbench.Features.Core;

/// <summary>
/// Simple audit logging service for demonstration purposes.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Logs an informational message.
    /// </summary>
    void LogInfo(string message);
}
