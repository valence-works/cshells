using CShells.SampleApp.Features.Core;

namespace CShells.SampleApp.Features.Admin;

/// <summary>
/// Implementation of the admin service.
/// </summary>
public class AdminService(ITimeService timeService, ShellSettings shellSettings) : IAdminService
{
    /// <inheritdoc />
    public AdminInfo GetAdminInfo()
    {
        return new(
            Status: "Running",
            ShellName: shellSettings.Id.Name,
            ServerTime: timeService.GetCurrentTime()
        );
    }
}