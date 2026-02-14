# CShells.FastEndpoints

FastEndpoints integration for CShells providing automatic endpoint discovery and registration for shell features.

## Purpose

This package integrates [FastEndpoints](https://fast-endpoints.com/) with CShells, allowing you to build high-performance APIs with per-shell endpoint isolation and configuration.

## Key Features

- **Automatic FastEndpoints discovery** - Endpoints are discovered from features implementing `IFastEndpointsShellFeature`
- **Per-shell endpoint isolation** - Each shell has its own set of FastEndpoints
- **Shell-scoped route prefix** - Configure `EndpointRoutePrefix` per shell for FastEndpoints-specific prefixing
- **Configurator support** - Implement `IFastEndpointsConfigurator` for custom FastEndpoints configuration

## Installation

```bash
dotnet add package CShells.FastEndpoints
```

## Quick Start

### 1. Create a FastEndpoints Feature

```csharp
using CShells.FastEndpoints.Features;
using CShells.Features;
using Microsoft.Extensions.DependencyInjection;

[ShellFeature("MyApi", DependsOn = ["FastEndpoints"])]
public class MyApiFeature : IFastEndpointsShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMyService, MyService>();
    }
}
```

### 2. Create an Endpoint

```csharp
using FastEndpoints;

public class GetWeatherEndpoint : EndpointWithoutRequest<WeatherResponse>
{
    public override void Configure()
    {
        Get("weather");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await SendAsync(new WeatherResponse { Temperature = 72 });
    }
}
```

### 3. Configure Shell

```json
{
  "CShells": {
    "Shells": [
      {
        "Name": "Default",
        "Features": ["Core", "FastEndpoints", "MyApi"],
        "Configuration": {
          "WebRouting": {
            "Path": "",
            "RoutePrefix": "api/v1"
          },
          "FastEndpoints": {
            "EndpointRoutePrefix": "fe"
          }
        }
      }
    ]
  }
}
```

With this configuration, the weather endpoint is accessible at `/api/v1/fe/weather`.

## Configuration

### Route Prefixes

CShells.FastEndpoints supports two levels of route prefixing:

| Configuration Key | Scope | Description |
|------------------|-------|-------------|
| `WebRouting:RoutePrefix` | All endpoints | Applied to all shell endpoints (minimal APIs, controllers, FastEndpoints) |
| `FastEndpoints:EndpointRoutePrefix` | FastEndpoints only | Applied specifically to FastEndpoints via `config.Endpoints.RoutePrefix` |

### Custom Configurators

Implement `IFastEndpointsConfigurator` to customize FastEndpoints configuration:

```csharp
public class MyConfigurator : IFastEndpointsConfigurator
{
    public void Configure(Config config)
    {
        config.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    }
}
```

Register in your feature:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IFastEndpointsConfigurator, MyConfigurator>();
}
```

## Related Packages

- **CShells.FastEndpoints.Abstractions** - Interfaces for feature libraries (reference this in your feature projects)
- **CShells.AspNetCore** - Required for web routing and endpoint registration

## Further Reading

- [FastEndpoints Documentation](https://fast-endpoints.com/)
- [CShells Feature Configuration](https://github.com/sfmskywalker/cshells/blob/main/docs/feature-configuration.md)

