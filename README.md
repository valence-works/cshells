[![Packages](https://github.com/sfmskywalker/cshells/actions/workflows/publish.yml/badge.svg)](https://github.com/sfmskywalker/cshells/actions/workflows/publish.yml)
[![NuGet CShells](https://img.shields.io/nuget/v/CShells.svg)](https://www.nuget.org/packages/CShells)
[![Docs](https://img.shields.io/badge/docs-cshells.io-blue)](https://www.cshells.io/)

![Target Framework](https://img.shields.io/badge/.NET-10-blueviolet)
[![License](https://img.shields.io/github/license/sfmskywalker/cshells.svg)](https://github.com/sfmskywalker/cshells/blob/main/LICENSE)
[![NuGet Downloads](https://img.shields.io/nuget/dt/CShells.svg)](https://www.nuget.org/packages/CShells)

# CShells

A lightweight, extensible shell and feature system for .NET projects that lets you build modular and multi-tenant apps with per-shell DI containers and config-driven features.

## Features

- **Multi-shell architecture** - Each shell has its own isolated DI container
- **Feature-based modularity** - Features are discovered automatically via attributes
- **Dependency resolution** - Features can depend on other features with topological ordering
- **Configuration-driven** - Shells and their features are configured via appsettings.json
- **ASP.NET Core integration** - Middleware for per-request shell resolution

## Use Cases

CShells is useful whenever you want clear modular boundaries, configurable feature sets, and isolated dependency graphs inside a .NET application.

### Modular Monoliths with Pluggable Features

Model each functional area (e.g., `Core`, `Billing`, `Reporting`) as a feature and group them into shells that can be enabled or disabled via configuration. This keeps a monolithic codebase modular and lets you turn features on or off without code changes.

### Multitenant Apps with Per-Tenant Feature Toggles

Treat each tenant as a shell with its own configuration and feature set. You can roll out features gradually, offer different capabilities per tenant, and keep tenant-specific services (e.g., integrations, branding, limits) isolated in per-shell DI containers.

### Single-Tenant Apps with Environment- or Plan-Based Features

Use shells to represent different plans (Basic, Pro, Enterprise) or environments (Development, Staging, Production), each enabling a different set of features. This lets you keep one codebase while varying behavior and dependencies based on environment, subscription level, or other criteria.

### Modular Frameworks and Platforms (CMS, CRM, Orchard Core/ABP-like)

Build your own modular application framework where modules are implemented as features discovered at startup. CShells’ feature discovery and ordering, combined with per-shell DI, make it a good fit for CMSs, CRMs, ERP-style systems, and frameworks similar to Orchard Core or ABP.

### White-Label SaaS and Branded Deployments

Model each brand or deployment as a shell with its own enabled features, configuration, and DI registrations. You can share the same core features while varying branding, integrations, or compliance-related components per shell.

### Extensible Line-of-Business Apps with Plugins

Expose extension points as features that can be discovered from additional assemblies and loaded into shells. This enables plugin-style architectures where internal teams or third parties can add capabilities without modifying the core app.

### API Gateways and Backend-for-Frontend (BFF) Layers

Use shells to represent different API surfaces (mobile, web, partner, admin) with their own middleware, endpoints, and policies. Each shell can have tailored dependencies and configuration while still sharing common infrastructure and hosting.

### Gradual Modularization of Legacy Apps

Introduce CShells into an existing application and start moving functionality into features and shells incrementally. This allows you to modularize and isolate areas of a legacy system over time without a big-bang rewrite.

## Packages

CShells provides multiple NuGet packages for different use cases:

| Package | Description | When to Use |
|---------|-------------|-------------|
| **CShells.Abstractions** | Core interfaces and models (`IShellFeature`, `ShellSettings`, `ShellId`) | Reference this in **feature class libraries** to avoid depending on the full framework |
| **CShells.AspNetCore.Abstractions** | ASP.NET Core interfaces (`IWebShellFeature`) | Reference this in **ASP.NET Core feature class libraries** for web endpoint support |
| **CShells** | Core framework implementation | Reference this in your **main application project** |
| **CShells.AspNetCore** | ASP.NET Core integration (middleware, routing, resolvers) | Reference this in your **ASP.NET Core application project** |
| **CShells.Providers.FluentStorage** | FluentStorage-based shell configuration provider | Use when loading shell configurations from disk, cloud storage, etc. |

### Recommended Project Structure

```
YourSolution/
├── src/
│   ├── YourApp/                          # Main ASP.NET Core application
│   │   └── YourApp.csproj                # References: CShells, CShells.AspNetCore, YourApp.Features
│   └── YourApp.Features/                 # Feature definitions library
│       └── YourApp.Features.csproj       # References: CShells.AspNetCore.Abstractions only
```

This structure allows your feature library to remain lightweight with minimal dependencies, while your main application references the full CShells implementation.

## Quick Start

### 1. Create a Feature

Features implement `IShellFeature` for service registration:

```csharp
using CShells.Features;
using Microsoft.Extensions.DependencyInjection;

public class CoreFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ITimeService, TimeService>();
    }
}
```

For ASP.NET Core applications with HTTP endpoints, implement `IWebShellFeature`:

```csharp
using CShells.AspNetCore.Features;
using Microsoft.Extensions.DependencyInjection;

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

**Best Practice:** Define your features in a separate class library that only references `CShells.Abstractions` (or `CShells.AspNetCore.Abstractions` for web features). This keeps your feature definitions lightweight and independent of the full framework implementation.

**The `[ShellFeature]` attribute is optional.** Use it only when you need to:
- Specify an explicit feature name (otherwise class name is used)
- Provide a display name
- Declare feature dependencies
- Set metadata

```csharp
using CShells.Features;

// Without attribute - feature name is "WeatherFeature" (derived from class name)
public class WeatherFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IWeatherService, WeatherService>();
    }
}

// With attribute - explicit name "Weather", display name, and string-based dependency
[ShellFeature("Weather", DisplayName = "Weather API", DependsOn = ["Core"])]
public class WeatherFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IWeatherService, WeatherService>();
    }
}

// Strongly-typed dependency - the feature name is resolved from CoreFeature's attribute
[ShellFeature("Weather", DisplayName = "Weather API", DependsOn = [typeof(CoreFeature)])]
public class WeatherFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IWeatherService, WeatherService>();
    }
}

// Mixed - combine string and type-based dependencies
[ShellFeature("Weather", DisplayName = "Weather API", DependsOn = [typeof(CoreFeature), "Logging"])]
public class WeatherFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IWeatherService, WeatherService>();
    }
}
```

> **Note:** When using `typeof(SomeFeature)` in `DependsOn`, the type must implement `IShellFeature`. The feature name is resolved from the target type's `[ShellFeature]` attribute (or derived from the class name if no attribute is present), so renaming the attribute automatically updates all dependents.

Features can access shell configuration via `IConfiguration` (resolved from the shell's service provider):

```csharp
using CShells;
using CShells.Features;
using Microsoft.Extensions.Configuration;

public class WeatherFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IWeatherService>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var apiKey = config["Weather:ApiKey"];
            return new WeatherService(apiKey);
        });
    }
}
```

### 2. Configure Shells

**Option A: Using appsettings.json** (default section name: `CShells`):

```json
{
  "CShells": {
    "Shells": {
      "Default": {
        "Features": {
          "Core": true,
          "Weather": true
        },
        "Configuration": {
          "WebRouting": {
            "Path": ""
          }
        }
      },
      "Admin": {
        "Features": {
          "Core": true,
          "Admin": { "MaxUsers": 100, "EnableAuditLog": true }
        },
        "Configuration": {
          "WebRouting": {
            "Path": "admin",
            "RoutePrefix": "api/v1"
          }
        }
      }
    }
  }
}
```

Shell names are the keys under `CShells:Shells`. Named paths remain stable when
shell entries are reordered, so an override continues to target the same shell.
For example:

```bash
CSHELLS__SHELLS__DEFAULT__FEATURES__IDENTITY__SIGNINGKEY=...
```

Use PascalCase shell names in JSON, such as `Default` or `MyShell`. In
environment variable paths, use the same shell-name segment without inserting
underscores between words, for example:

```bash
CSHELLS__SHELLS__MYSHELL__FEATURES__IDENTITY__SIGNINGKEY=...
```

Environment variable keys are commonly written in uppercase.

You can also override the configuration section name via `builder.AddShells("MySection")`.

**Option B: Using JSON files with FluentStorage**:

Create JSON files in a `Shells` folder (e.g., `Default.json`, `Admin.json`):

```json
{
  "Name": "Default",
  "Features": {
    "Core": true,
    "Weather": true
  },
  "Configuration": {
    "WebRouting": {
      "Path": ""
    }
  }
}
```

Then configure the provider:

```csharp
using FluentStorage;
using CShells.Providers.FluentStorage;

var builder = WebApplication.CreateBuilder(args);
var shellsPath = Path.Combine(builder.Environment.ContentRootPath, "Shells");
var blobStorage = StorageFactory.Blobs.DirectoryFiles(shellsPath);

builder.AddShells(cshells =>
{
    cshells.WithFluentStorageProvider(blobStorage);
});
```

**Option C: Code-first configuration**:

```csharp
builder.AddShells(cshells =>
{
    cshells.AddShell("Default", shell => shell
        .WithFeatures("Core", "Weather")
        .WithConfiguration("WebRouting:Path", ""));

    cshells.AddShell("Admin", shell => shell
        .WithFeature("Core")
        .WithFeature("Admin", settings => settings
            .WithSetting("MaxUsers", 100)
            .WithSetting("EnableAuditLog", true))
        .WithConfiguration("WebRouting:Path", "admin")
        .WithConfiguration("WebRouting:RoutePrefix", "api/v1"));
});
```

### 3. Register CShells in Program.cs

**Simple setup (reads from appsettings.json)**:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register CShells from configuration (default section: CShells)
// Uses the host-derived default feature assembly set
builder.AddShells();

var app = builder.Build();

// Configure middleware and endpoints for all shells
app.MapShells();

app.Run();
```

To switch to explicit feature assembly selection, configure it fluently on `CShellsBuilder`:

Use `WithAssemblies(...)` and `WithHostAssemblies()` to select which assemblies feature discovery should scan, and `WithAssemblyProvider(...)` when you want to attach a provider that contributes assemblies.

```csharp
builder.AddShells(cshells =>
{
    cshells.WithConfigurationProvider(builder.Configuration);
    cshells.WithAssemblies(typeof(Program).Assembly);
});
```

Feature assembly selection is additive:

```csharp
builder.AddShells(cshells =>
{
    cshells.WithConfigurationProvider(builder.Configuration);

    // Explicit developer-supplied assemblies
    cshells.WithAssemblies(typeof(Program).Assembly);

    // Re-include the built-in host-derived default when explicit mode is active
    cshells.WithHostAssemblies();

    // Append a custom discovery source
    cshells.WithAssemblyProvider(sp =>
        new MyFeatureAssemblyProvider(sp.GetRequiredService<IModuleCatalog>()));
});
```

If you do not call any assembly-source method or shared assembly selector, CShells preserves the default host-derived discovery behavior. As soon as you call `WithAssemblies(...)`, `WithHostAssemblies()`, or `WithAssemblyProvider(...)`, CShells switches to explicit provider mode and scans only the assemblies contributed by those appended providers.

Shared assembly selectors let a host include framework families without listing every assembly. When no explicit assembly providers are configured, `CShells:SharedAssemblies` narrows the default host-derived discovery set to matching assemblies. When explicit providers are configured, matching host-derived assemblies are added to the explicit provider results and deduplicated.

```json
{
  "CShells": {
    "SharedAssemblies": [
      "Elsa",
      "Elsa.*"
    ],
    "Shells": {
      "Default": {
        "Features": {
          "Core": true
        }
      }
    }
  }
}
```

Entries without `*` match exact assembly simple names. Entries ending in `*` match simple-name prefixes, so `Elsa.*` matches `Elsa.Workflows` but not `Contoso.Workflows`. The wildcard is only valid as the final character. Use narrow framework contract or common infrastructure patterns; broad sharing can weaken shell isolation.

Integration packages can contribute the same host-wide selectors in code:

```csharp
builder.AddShells(cshells =>
{
    cshells.WithSharedAssemblies("Elsa", "Elsa.*");
    cshells.WithSharedAssembliesWhere(name =>
        name.StartsWith("MyFramework.", StringComparison.OrdinalIgnoreCase));
    cshells.WithConfigurationProvider(builder.Configuration);
});
```

**FluentStorage setup (reads from Shells folder)**:

```csharp
using FluentStorage;
using CShells.Providers.FluentStorage;

var builder = WebApplication.CreateBuilder(args);
var shellsPath = Path.Combine(builder.Environment.ContentRootPath, "Shells");
var blobStorage = StorageFactory.Blobs.DirectoryFiles(shellsPath);

builder.AddShells(cshells =>
{
    cshells.WithFluentStorageProvider(blobStorage);
});

var app = builder.Build();
app.MapShells();
app.Run();
```

**Advanced setup with custom resolvers**:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCShellsAspNetCore(cshells =>
{
    cshells.WithConfigurationProvider(builder.Configuration);
    cshells.WithWebRoutingResolver(options =>
    {
        // Configure web routing options
        options.ExcludePaths = new[] { "/api", "/health" };
        options.HeaderName = "X-Tenant-Id";
    });
});

var app = builder.Build();
app.MapShells();
app.Run();
```

### Key Capabilities

- **IShellFeature** - Basic interface for service registration in features
- **IWebShellFeature** - Extends `IShellFeature` to add HTTP endpoint registration via `MapEndpoints()`
- **Optional `[ShellFeature]` attribute** - Use only when you need explicit names, display names, dependencies, or metadata
- **Automatic endpoint routing** - `MapShells()` handles middleware and endpoint registration in one call
- **Shell path prefixes** - Routes are automatically prefixed based on `WebRouting:Path`
- **Route prefixes** - Apply additional route prefixes to all endpoints via `WebRouting:RoutePrefix`
- **Per-shell DI containers** - Each shell has its own isolated service provider with shell-specific services
- **Shell-scoped IConfiguration** - Each shell gets its own `IConfiguration` built from its `Configuration` section
- **Multiple configuration sources** - Configure shells via appsettings.json, external JSON files, or code
- **Flexible shell resolution** - Built-in path and host resolvers, plus extensibility for custom strategies
- **Feature dependencies** - Features can depend on other features with automatic topological ordering
- **Lifecycle ordering** - Initializers can use `AddShellInitializer<T>()`, semantic phases, and numeric order independently from feature dependency order
- **Constructor injection of ShellSettings** - Features can access their shell's configuration via constructor
- **Runtime shell management** - Add, update, or remove shells at runtime without restarting the application

See [Shell Lifecycle](docs/shell-lifecycle.md) for initializer ordering, provider/base feature patterns, drain behavior, and compatibility guidance for existing `IShellInitializer` registrations.

## Configuration

### Shell Settings Providers

CShells supports multiple ways to configure shells:

#### 1. Configuration-based (appsettings.json)

```csharp
builder.AddShells(); // Uses default "CShells" section
// or
builder.AddShells("MyCustomSection");
// or
builder.Services.AddCShellsAspNetCore(cshells =>
{
    cshells.WithConfigurationProvider(builder.Configuration, "CShells");
});
```

#### 2. FluentStorage (JSON files from disk/cloud)

```csharp
using CShells.Providers.FluentStorage;

var blobStorage = StorageFactory.Blobs.DirectoryFiles("./Shells");
builder.AddShells(cshells =>
{
    cshells.WithFluentStorageProvider(blobStorage);
});
```

#### 3. Code-first (In-memory)

```csharp
builder.AddShells(cshells =>
{
    cshells.AddShell("Default", shell => shell
        .WithFeatures("Core", "Weather")
        .WithConfiguration("WebRouting:Path", "")
        .WithConfiguration("Theme", "Dark"));
});
```

#### 4. Custom Blueprint Provider

```csharp
public class DatabaseShellBlueprintProvider : IShellBlueprintProvider
{
    public async Task<ProvidedBlueprint?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        // Load from database, API, etc.
        var settings = await LoadSettingsAsync(name, cancellationToken);
        return settings is null ? null : new ProvidedBlueprint(new DatabaseShellBlueprint(settings));
    }

    public Task<BlueprintPage> ListAsync(BlueprintListQuery query, CancellationToken cancellationToken = default) =>
        // Return paged BlueprintSummary rows for your source.
        throw new NotImplementedException();
}

builder.AddShells(cshells =>
{
    cshells.AddBlueprintProvider(sp => sp.GetRequiredService<DatabaseShellBlueprintProvider>());
});
```

`DatabaseShellBlueprint` is your `IShellBlueprint` implementation that composes fresh `ShellSettings` for a tenant.

## Shell Scopes & Background Work

Shell scopes provide a way to create scoped services within a shell's service provider. This is particularly useful for background workers or other services that need to execute work in the context of each shell.

### Creating Shell Scopes

Use `IShellRegistry` to select active shells, then call `IShell.BeginScope()` to create a tracked scope for a shell:

```csharp
using CShells;
using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

public class MyService(IShellRegistry registry)
{
    public async Task DoWorkAsync()
    {
        foreach (var shell in registry.GetActiveShells())
        {
            await using var scope = shell.BeginScope();

            // Resolve scoped services from the shell's service provider
            var myService = scope.ServiceProvider.GetRequiredService<IMyService>();
            await myService.ExecuteAsync();
        }
    }
}
```

### Background Worker Example

Here's an example of a background service that executes work for each shell:

```csharp
using CShells;
using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class ShellBackgroundWorker(
    IShellRegistry registry,
    ILogger<ShellBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var shell in registry.GetActiveShells())
            {
                await using var scope = shell.BeginScope();

                // Execute work in the shell's context
                logger.LogInformation("Background work executed for shell '{Shell}'", shell.Descriptor);

                // Resolve and use scoped services
                var service = scope.ServiceProvider.GetService<IMyService>();
                if (service is not null)
                    await service.ExecuteAsync(stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

Register your background worker in your service collection:

```csharp
services.AddHostedService<ShellBackgroundWorker>();
```

## Running the Sample App

The `samples/CShells.Workbench` project demonstrates a multi-tenant payment platform:

```bash
cd samples/CShells.Workbench
dotnet run
```

Then access (actual ports depend on your Kestrel/HTTPS dev cert setup):
- `https://localhost:5001/` - Default tenant (Basic tier - Stripe + Email)
- `https://localhost:5001/acme` - Acme Corp (Premium tier - PayPal + SMS + Fraud Detection)
- `https://localhost:5001/contoso` - Contoso Ltd (Enterprise tier - Stripe + Multi-channel + Fraud + Reporting)
- `https://localhost:5001/swagger` - Swagger UI for all endpoints

See the [Workbench README](samples/CShells.Workbench/README.md) for detailed feature descriptions and API examples.

## License

MIT License - see [LICENSE](LICENSE) for details.
