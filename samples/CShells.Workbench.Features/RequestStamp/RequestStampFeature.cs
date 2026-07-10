using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CShells.Workbench.Features.RequestStamp;

/// <summary>
/// Request-stamp feature — demonstrates the CShells middleware seam
/// (<see cref="IMiddlewareShellFeature"/>). Mounts an <see cref="IMiddleware"/>-style middleware
/// into the shell's pipeline that stamps every response with an <c>X-Shell-Stamp</c> header
/// carrying the tenant name. The middleware is resolved per request from the shell scope
/// (via the <see cref="IMiddlewareFactory"/> CShells guarantees in shell containers), so it can
/// inject shell-scoped services — here, the shell's <see cref="ShellSettings"/>.
/// </summary>
[ShellFeature("RequestStamp", DisplayName = "Request Stamp", Description = "Stamps responses with an X-Shell-Stamp header carrying the tenant name.")]
public class RequestStampFeature : IMiddlewareShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<RequestStampMiddleware>();
    }

    public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment)
    {
        app.UseMiddleware<RequestStampMiddleware>();
    }
}

/// <summary>Stamps the response with the owning shell's name.</summary>
public class RequestStampMiddleware(ShellSettings settings) : IMiddleware
{
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Shell-Stamp"] = settings.Id.Name;
            return Task.CompletedTask;
        });
        return next(context);
    }
}
