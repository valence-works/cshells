# CShells

A lightweight shell & feature system for .NET projects that lets you build modular and multi-tenant apps with per-shell DI containers and config-driven features.

## Features

- **Multi-shell architecture** - Each shell has its own isolated DI container
- **Feature-based modularity** - Features are discovered automatically via attributes
- **Dependency resolution** - Features can depend on other features with topological ordering
- **Configuration-driven** - Shells and their features are configured via appsettings.json
- **ASP.NET Core integration** - Middleware for per-request shell resolution

## Quick Start

### 1. Create a Feature

Features implement `IShellFeature` and are decorated with `[ShellFeature]`:

```csharp
using CShells;
using Microsoft.Extensions.DependencyInjection;

[ShellFeature("Core", DisplayName = "Core Services")]
public class CoreFeatureStartup : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ITimeService, TimeService>();
    }
}
```

Features can depend on other features:

```csharp
[ShellFeature("Weather", DependsOn = ["Core"], DisplayName = "Weather Feature")]
public class WeatherFeatureStartup : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IWeatherService, WeatherService>();
    }
}
```

### 2. Configure Shells

Add shell configuration to `appsettings.json`:

```json
{
  "CShells": {
    "Shells": [
      {
        "Name": "Default",
        "Features": [ "Core", "Weather" ],
        "Properties": {
          "Title": "Default site"
        }
      },
      {
        "Name": "Admin",
        "Features": [ "Core", "Admin" ],
        "Properties": {
          "Title": "Admin area"
        }
      }
    ]
  }
}
```

### 3. Register CShells in Program.cs

```csharp
using CShells;
using CShells.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Register CShells services from configuration
builder.Services.AddCShells(
    builder.Configuration, 
    assemblies: [typeof(Program).Assembly]);

// Register ASP.NET Core integration
builder.Services.AddCShellsAspNetCore();

var app = builder.Build();

// Enable shell resolution middleware
app.UseCShells();

// Resolve services from the current shell's container
app.MapGet("/", (HttpContext context) =>
{
    var weatherService = context.RequestServices.GetRequiredService<IWeatherService>();
    return Results.Ok(weatherService.GetForecast());
});

app.Run();
```

## Running the Sample App

The `samples/CShells.SampleApp` project demonstrates a complete CShells integration:

```bash
cd samples/CShells.SampleApp
dotnet run
```

Then access:
- `http://localhost:5000/` - Default shell (Weather feature)
- `http://localhost:5000/admin` - Admin shell (Admin feature)

## Documentation

See [src/CShells/README.md](src/CShells/README.md) for detailed configuration and usage documentation.

## License

MIT License - see [LICENSE](LICENSE) for details.

