using CShells;
using CShells.AspNetCore;
using CShells.AspNetCore.Resolvers;
using CShells.SampleApp.Features;

var builder = WebApplication.CreateBuilder(args);

// Register CShells services from configuration
builder.Services.AddCShells(
    builder.Configuration, 
    assemblies: [typeof(Program).Assembly]);

// Register ASP.NET Core integration for CShells with a custom shell resolver
// The PathShellResolver maps paths to specific shells, defaults to Default shell otherwise
builder.Services.AddSingleton<IShellResolver>(sp =>
{
    var pathMappings = new Dictionary<string, ShellId>
    {
        ["admin"] = new("Admin"),
        ["tropical"] = new("Tropical")
    };
    return new CompositeShellResolver(
        new PathShellResolver(pathMappings),
        new DefaultShellIdResolver());
});
builder.Services.AddCShellsAspNetCore();

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

app.Run();

/// <summary>
/// A composite shell resolver that tries multiple resolvers in order.
/// </summary>
file class CompositeShellResolver(params IShellResolver[] resolvers) : IShellResolver
{
    public ShellId? Resolve(Microsoft.AspNetCore.Http.HttpContext httpContext) =>
        resolvers
            .Select(resolver => resolver.Resolve(httpContext))
            .FirstOrDefault(shellId => shellId.HasValue);
}

/// <summary>
/// A resolver that always returns the Default shell.
/// </summary>
file class DefaultShellIdResolver : IShellResolver
{
    public ShellId? Resolve(Microsoft.AspNetCore.Http.HttpContext httpContext) 
        => new ShellId("Default");
}

