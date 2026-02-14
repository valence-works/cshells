# CShells.AspNetCore

ASP.NET Core integration for CShells providing middleware and shell resolution based on HTTP context.

## Purpose

This package provides ASP.NET Core middleware, shell resolution strategies, and extensions for building multi-tenant web applications with CShells.

## Key Features

- **HTTP middleware for shell resolution** - Automatic per-request tenant/shell detection
- **Host-based tenant resolution** - Route requests based on hostname
- **Path-based tenant resolution** - Route requests based on URL path prefix
- **`MapShells()` extension** - Automatically register endpoints from all shell features
- **Web routing configuration** - Configure path prefixes and routing options per shell
- **Custom shell resolvers** - Extensibility for custom resolution strategies

## Installation

```bash
dotnet add package CShells
dotnet add package CShells.AspNetCore
```

## Quick Start

### 1. Create a Web Feature

```csharp
using CShells.AspNetCore.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[ShellFeature("Weather", DisplayName = "Weather API")]
public class WeatherFeature : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IWeatherService, WeatherService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        endpoints.MapGet("weather", (IWeatherService weatherService) =>
            weatherService.GetForecast());
    }
}
```

### 2. Configure Shells with Web Routing

```json
{
  "CShells": {
    "Shells": [
      {
        "Name": "Default",
        "Features": ["Weather"],
        "Configuration": {
          "WebRouting": {
            "Path": ""
          }
        }
      },
      {
        "Name": "Admin",
        "Features": ["Admin"],
        "Configuration": {
          "WebRouting": {
            "Path": "admin",
            "RoutePrefix": "api/v1"
          }
        }
      }
    ]
  }
}
```

### 3. Register CShells in Program.cs

**Simple setup:**

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register CShells from configuration
builder.AddShells();

var app = builder.Build();

// Configure middleware and endpoints for all shells
app.MapShells();

app.Run();
```

**Advanced setup with custom resolvers:**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCShellsAspNetCore(cshells =>
{
    cshells.WithConfigurationProvider(builder.Configuration);
    cshells.WithWebRoutingResolver(options =>
    {
        options.ExcludePaths = new[] { "/api", "/health" };
        options.HeaderName = "X-Tenant-Id";
    });
});

var app = builder.Build();
app.MapShells();
app.Run();
```

## Shell Resolution Strategies

### Path-Based Resolution

Shells are resolved based on the URL path prefix:

```json
{
  "Configuration": {
    "WebRouting": {
      "Path": "admin"
    }
  }
}
```

Requests to `/admin/*` will be routed to this shell.

### Host-Based Resolution

Configure shells to respond to specific hostnames:

```json
{
  "Configuration": {
    "WebRouting": {
      "Host": "admin.example.com"
    }
  }
}
```

### Route Prefix

Apply a route prefix to all endpoints in a shell:

```json
{
  "Configuration": {
    "WebRouting": {
      "Path": "tenant1",
      "RoutePrefix": "api/v1"
    }
  }
}
```

With this configuration, an endpoint mapped at `weather` will be accessible at `/tenant1/api/v1/weather`.

### Custom Resolution

Implement `IShellResolver` for custom resolution logic:

```csharp
public class CustomShellResolver : IShellResolver
{
    public Task<string?> ResolveShellIdAsync(HttpContext context)
    {
        // Custom logic to determine shell ID from HTTP context
        var shellId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        return Task.FromResult(shellId);
    }
}

builder.Services.AddCShellsAspNetCore(cshells =>
{
    cshells.WithResolver<CustomShellResolver>();
});
```

## Web Routing Options

Configure routing behavior:

```csharp
builder.Services.AddCShellsAspNetCore(cshells =>
{
    cshells.WithWebRoutingResolver(options =>
    {
        // Exclude paths from shell resolution
        options.ExcludePaths = new[] { "/api", "/health", "/swagger" };

        // Use custom header for tenant ID
        options.HeaderName = "X-Tenant-Id";

        // Configure default shell
        options.DefaultShell = "Default";
    });
});
```

## Learn More

- [Main Documentation](https://github.com/sfmskywalker/cshells)
- [Sample Application](../../samples/CShells.Workbench)
- [CShells Package](../CShells) - Core runtime
