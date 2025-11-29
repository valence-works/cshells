using CShells.AspNetCore;

namespace CShells.SampleApp.Features.Greeting;

/// <summary>
/// Greetings feature that demonstrates both <see cref="IShellFeature"/> and <see cref="IWebShellFeature"/>.
/// </summary>
/// <remarks>
/// <para>
/// This feature:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     Registers a shell-scoped <see cref="IGreetingService"/> via <see cref="IShellFeature.ConfigureServices"/>.
///     </description>
///   </item>
///   <item>
///     <description>
///     Configures a <c>/greetings</c> endpoint via <see cref="IWebShellFeature.Configure"/> that uses
///     the shell-scoped greeting service at request time.
///     </description>
///   </item>
/// </list>
/// </remarks>
[ShellFeature("Greetings", DependsOn = ["Core"], DisplayName = "Greetings Feature")]
public class GreetingsFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IGreetingService, GreetingService>();
    }

    /// <inheritdoc />
    public void Configure(IApplicationBuilder app, IHostEnvironment environment)
    {
        // Map a simple endpoint that uses the shell-scoped greeting service.
        // The service is resolved from the current shell's service provider
        // (set by ShellMiddleware based on the request).
        app.Map("/greetings", greetingsApp =>
        {
            greetingsApp.Run(async context =>
            {
                var greetingService = context.RequestServices.GetRequiredService<IGreetingService>();
                var greeting = greetingService.GetGreeting();

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    Message = greeting,
                    Timestamp = DateTime.UtcNow
                });
            });
        });
    }
}