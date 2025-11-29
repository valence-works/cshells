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
