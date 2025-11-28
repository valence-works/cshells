using CShells;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.SampleApp.Features;

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

/// <summary>
/// Admin information record.
/// </summary>
public record AdminInfo(string Status, string ShellName, DateTime ServerTime);

/// <summary>
/// Implementation of the admin service.
/// </summary>
public class AdminService : IAdminService
{
    private readonly ITimeService _timeService;
    private readonly ShellSettings _shellSettings;

    public AdminService(ITimeService timeService, ShellSettings shellSettings)
    {
        _timeService = timeService;
        _shellSettings = shellSettings;
    }

    /// <inheritdoc />
    public AdminInfo GetAdminInfo()
    {
        return new AdminInfo(
            Status: "Running",
            ShellName: _shellSettings.Id.Name,
            ServerTime: _timeService.GetCurrentTime()
        );
    }
}

/// <summary>
/// Admin feature that registers administrative services.
/// </summary>
[ShellFeature("Admin", DependsOn = ["Core"], DisplayName = "Admin Feature")]
public class AdminFeature : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IAdminService, AdminService>();
    }
}
