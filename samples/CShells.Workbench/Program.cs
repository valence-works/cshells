using CShells.AspNetCore.Extensions;
using CShells.AspNetCore.Resolution;
using CShells.Workbench.Background;

var builder = WebApplication.CreateBuilder(args);
builder.AddShells();

// Configure header-based routing after the fact by replacing the options
var services = builder.Services;
var optionsDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(WebRoutingShellResolverOptions));
if (optionsDescriptor != null)
{
    services.Remove(optionsDescriptor);
    var newOptions = new WebRoutingShellResolverOptions { HeaderName = "X-Tenant-Id" };
    services.AddSingleton(newOptions);
}

// Register background work observer
builder.Services.AddSingleton<IBackgroundWorkObserver, ConsoleBackgroundWorkObserver>();

// Register background worker
builder.Services.AddHostedService<ShellDemoWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.MapShells();

app.Run();

// Make Program class accessible for WebApplicationFactory
namespace CShells.Workbench
{
    public partial class Program;
}