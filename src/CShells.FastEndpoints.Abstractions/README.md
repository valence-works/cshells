# CShells.FastEndpoints.Abstractions

Abstractions for FastEndpoints integration with CShells.

## Purpose

This package provides the marker interface `IFastEndpointsShellFeature` that feature libraries can implement to indicate they contain FastEndpoints. Reference this package in your feature class libraries to avoid depending on the full FastEndpoints framework.

## Key Interfaces

- **`IFastEndpointsShellFeature`** - Marker interface for features containing FastEndpoints
- **`IFastEndpointsConfigurator`** - Interface for customizing FastEndpoints configuration

## Installation

```bash
dotnet add package CShells.FastEndpoints.Abstractions
```

## Usage

### Feature Library

In your feature class library, implement `IFastEndpointsShellFeature`:

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

### Custom Configurator

Implement `IFastEndpointsConfigurator` to customize FastEndpoints:

```csharp
using CShells.FastEndpoints.Contracts;
using FastEndpoints;

public class MyConfigurator : IFastEndpointsConfigurator
{
    public void Configure(Config config)
    {
        config.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    }
}
```

## Project Structure

```
YourSolution/
├── src/
│   ├── YourApp/                          # Main ASP.NET Core application
│   │   └── YourApp.csproj                # References: CShells.FastEndpoints
│   └── YourApp.Features/                 # Feature definitions library
│       └── YourApp.Features.csproj       # References: CShells.FastEndpoints.Abstractions
```

## Related Packages

- **CShells.FastEndpoints** - Full implementation (reference in main application)
- **CShells.AspNetCore.Abstractions** - For `IWebShellFeature` interface

