using CShells;
using CShells.AspNetCore;
using CShells.AspNetCore.Resolvers;
using CShells.SampleApp;
using CShells.SampleApp.Features.Admin;
using CShells.SampleApp.Features.Core;
using CShells.SampleApp.Features.Greeting;
using CShells.SampleApp.Features.Weather;

var builder = WebApplication.CreateBuilder(args);

// Register CShells services and ASP.NET Core integration using a single entry point
// and configure a custom shell resolver.
builder.AddCShells(services =>
{
    var pathMappings = new Dictionary<string, ShellId>
    {
        ["admin"] = new("Admin"),
        ["tropical"] = new("Tropical")
    };

    services.AddSingleton<IShellResolver>(_ =>
        new CompositeShellResolver(
            new PathShellResolver(pathMappings),
            new DefaultShellIdResolver()));
}, assemblies: [typeof(Program).Assembly]);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable shell resolution middleware - this sets HttpContext.RequestServices 
// to the shell's service provider based on the request path
app.UseCShells();

// Root endpoint - resolves to Default shell with Weather feature
app.MapGet("/", (HttpContext context) =>
    {
        var timeService = context.RequestServices.GetRequiredService<ITimeService>();
        var weatherService = context.RequestServices.GetRequiredService<IWeatherService>();
    
        return Results.Ok(new
        {
            Shell = "Default",
            CurrentTime = timeService.GetCurrentTime(),
            Forecast = weatherService.GetForecast()
        });
    })
    .WithName("GetDefaultHome");

// Tropical endpoint - resolves to Tropical shell with TropicalWeather feature
app.MapGet("/tropical", (HttpContext context) =>
    {
        var timeService = context.RequestServices.GetRequiredService<ITimeService>();
        var weatherService = context.RequestServices.GetRequiredService<IWeatherService>();

        return Results.Ok(new
        {
            Shell = "Tropical",
            CurrentTime = timeService.GetCurrentTime(),
            Forecast = weatherService.GetForecast()
        });
    })
    .WithName("GetTropicalHome");

// Admin endpoint - resolves to Admin shell with Admin feature
app.MapGet("/admin", (HttpContext context) =>
    {
        var adminService = context.RequestServices.GetRequiredService<IAdminService>();

        return Results.Ok(new
        {
            Shell = "Admin",
            AdminInfo = adminService.GetAdminInfo()
        });
    })
    .WithName("GetAdminHome");

// Greet endpoint - uses IGreetingService from the resolved shell
app.MapGetWithShellPrefix("greet", (HttpContext context, string shellPath) =>
    {
        var greetingService = context.RequestServices.GetRequiredService<IGreetingService>();

        return Results.Ok(new
        {
            ShellPath = shellPath,
            Greeting = greetingService.GetGreeting()
        });
    })
    .WithName("GetGreeting");

app.Run();

namespace CShells.SampleApp
{

    /// <summary>
    /// A resolver that always returns the Default shell.
    /// </summary>
    internal sealed class DefaultShellIdResolver : IShellResolver
    {
        public ShellId? Resolve(HttpContext httpContext) 
            => new ShellId("Default");
    }
}