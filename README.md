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

## Quick Start

### 1. Create a Feature

Features implement `IShellFeature` (for service registration) or `IWebShellFeature` (for services + endpoints):

```csharp
using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.Extensions.DependencyInjection;

[ShellFeature("Core", DisplayName = "Core Services")]
public class CoreFeature : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ITimeService, TimeService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        endpoints.MapGet("", () => new { Message = "Hello from Core feature" });
    }
}
```

Features can depend on other features and access `ShellSettings` via constructor:

```csharp
[ShellFeature("Weather", DependsOn = ["Core"], DisplayName = "Weather Feature")]
public class WeatherFeature(ShellSettings shellSettings) : IWebShellFeature
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

### 2. Configure Shells

**Option A: Using appsettings.json** (default section name: `CShells`):

```json
{
  "CShells": {
    "Shells": [
      {
        "Name": "Default",
        "Features": [ "Core", "Weather" ],
        "Properties": {
          "WebRouting": {
            "Path": ""
          }
        }
      },
      {
        "Name": "Admin",
        "Features": [ "Core", "Admin" ],
        "Properties": {
          "WebRouting": {
            "Path": "admin"
          }
        }
      }
    ]
  }
}
```

You can also override the configuration section name via `builder.AddShells("MySection")`.

**Option B: Using JSON files with FluentStorage**:

Create JSON files in a `Shells` folder (e.g., `Default.json`, `Admin.json`):

```json
{
  "Name": "Default",
  "Features": [ "Core", "Weather" ],
  "Properties": {
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
        .WithPath(""));

    cshells.AddShell("Admin", shell => shell
        .WithFeatures("Core", "Admin")
        .WithPath("admin"));

    cshells.WithInMemoryShells();
});
```

### 3. Register CShells in Program.cs

**Simple setup (reads from appsettings.json)**:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register CShells from configuration (default section: CShells)
// Scans all loaded assemblies for features by default
builder.AddShells();

var app = builder.Build();

// Configure middleware and endpoints for all shells
app.MapShells();

app.Run();
```

You can optionally specify assemblies to scan for features:

```csharp
builder.AddShells(assemblies: [typeof(Program).Assembly]);
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

- **IWebShellFeature** - Features can expose their own endpoints using `MapEndpoints()`, keeping all logic self-contained
- **Automatic endpoint routing** - `MapShells()` handles middleware and endpoint registration in one call
- **Shell path prefixes** - Routes are automatically prefixed based on the `WebRouting.Path` property
- **Per-shell DI containers** - Each shell has its own isolated service provider with shell-specific services
- **Multiple configuration sources** - Configure shells via appsettings.json, external JSON files, or code
- **Flexible shell resolution** - Built-in path and host resolvers, plus extensibility for custom strategies
- **Feature dependencies** - Features can depend on other features with automatic topological ordering
- **Constructor injection of ShellSettings** - Features can access their shell's configuration via constructor
- **Runtime shell management** - Add, update, or remove shells at runtime without restarting the application


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
        .WithPath("")
        .WithProperty("Title", "Default Site"));

    cshells.WithInMemoryShells();
});
```

#### 4. Custom Provider

```csharp
public class DatabaseShellSettingsProvider : IShellSettingsProvider
{
    public async Task<IEnumerable<ShellSettings>> GetAllAsync()
    {
        // Load from database, API, etc.
        return Enumerable.Empty<ShellSettings>();
    }
}

builder.AddShells(cshells =>
{
    cshells.WithProvider<DatabaseShellSettingsProvider>();
});
```

## Shell Context Scopes & Background Work

Shell context scopes provide a way to create scoped services within a shell's service provider. This is particularly useful for background workers or other services that need to execute work in the context of each shell.

### Creating Shell Context Scopes

Use `IShellContextScopeFactory` to create scopes for shell contexts:

```csharp
using CShells;

public class MyService
{
    private readonly IShellHost _shellHost;
    private readonly IShellContextScopeFactory _scopeFactory;

    public MyService(IShellHost shellHost, IShellContextScopeFactory scopeFactory)
    {
        _shellHost = shellHost;
        _scopeFactory = scopeFactory;
    }

    public void DoWork()
    {
        foreach (var shell in _shellHost.AllShells)
        {
            using var scope = _scopeFactory.CreateScope(shell);

            // Resolve scoped services from the shell's service provider
            var myService = scope.ServiceProvider.GetRequiredService<IMyService>();
            myService.Execute();
        }
    }
}
```

### Background Worker Example

Here's an example of a background service that executes work for each shell:

```csharp
using CShells;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class ShellBackgroundWorker : BackgroundService
{
    private readonly IShellHost _shellHost;
    private readonly IShellContextScopeFactory _scopeFactory;
    private readonly ILogger<ShellBackgroundWorker> _logger;

    public ShellBackgroundWorker(
        IShellHost shellHost,
        IShellContextScopeFactory scopeFactory,
        ILogger<ShellBackgroundWorker> logger)
    {
        _shellHost = shellHost;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var shell in _shellHost.AllShells)
            {
                using var scope = _scopeFactory.CreateScope(shell);

                // Execute work in the shell's context
                _logger.LogInformation("Background work executed for shell '{ShellId}'", shell.Id.Name);

                // Resolve and use scoped services
                var service = scope.ServiceProvider.GetService<IMyService>();
                service?.Execute();
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
