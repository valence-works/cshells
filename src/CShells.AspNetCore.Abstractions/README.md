# CShells.AspNetCore.Abstractions

ASP.NET Core abstractions for building web shell features without dependencies on the full CShells framework.

## Purpose

This package contains ASP.NET Core-specific interfaces and models for building web features. By referencing only this package in your web feature libraries, you avoid pulling in the entire CShells runtime and its dependencies.

## When to Use

- Building ASP.NET Core feature libraries that will be consumed by CShells applications
- Creating reusable web features with HTTP endpoints
- Keeping feature library dependencies minimal while accessing web-specific abstractions

## Key Types

- `IWebShellFeature` - Interface for features that can register both services and HTTP endpoints
- Web-specific abstractions and models

## Installation

```bash
dotnet add package CShells.AspNetCore.Abstractions
```

## Example Usage

```csharp
using CShells.AspNetCore.Abstractions.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[ShellFeature("Api", DisplayName = "API Feature")]
public class ApiFeature : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IApiService, ApiService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        endpoints.MapGet("api/status", () => new { Status = "OK" });
    }
}
```

## Learn More

- [Main Documentation](https://github.com/sfmskywalker/cshells)
- [CShells.AspNetCore Package](../CShells.AspNetCore) - Full ASP.NET Core integration
