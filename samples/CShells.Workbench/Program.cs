using CShells.AspNetCore.Extensions;
using CShells.AspNetCore.Resolution;

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
public partial class Program;