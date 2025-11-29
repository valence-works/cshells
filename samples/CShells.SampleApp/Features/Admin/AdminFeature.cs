namespace CShells.SampleApp.Features.Admin;

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