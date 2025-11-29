namespace CShells.SampleApp.Features.Admin;

/// <summary>
/// Admin service interface for administrative operations.
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Gets admin dashboard information.
    /// </summary>
    AdminInfo GetAdminInfo();
}