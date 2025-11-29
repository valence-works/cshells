# CShells

A lightweight shell & feature system for .NET projects that lets you build modular and multi-tenant apps with per-shell DI containers and config-driven features.

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

Add shell configuration to `appsettings.json` (default section name: `CShells`):

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

Notes:
- Shell names are case-insensitive and must be unique; duplicate names will cause an exception during configuration.
- Feature names are trimmed and preserved in the order they are configured.

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

### Highlights

The following patterns are supported out of the box and are useful when getting started:

- **Flexible shell resolution**  Implement `IShellResolver` to choose the active shell per request (e.g., by path, host, header, or tenant id), and compose multiple resolvers to build more advanced strategies.
- **Per-shell DI on `HttpContext.RequestServices`**  After calling `app.UseCShells()`, `HttpContext.RequestServices` is automatically set to the current shells service provider, so you can resolve shell-specific services in minimal APIs or controllers without special plumbing.
- **Path-based shells / areas**  Map different URL segments to different shells (for example, a default public area, an admin area, or a specialized area), each with its own set of features and registrations.
- **Shell-aware routing helpers**  Use shell-aware mapping helpers (such as methods that add a shell path prefix) to build routes that automatically include shell context while still resolving services from the correct shell.
- **Different implementations per shell**  Register different implementations of the same interface per shell (e.g., one `IWeatherService` for a standard shell and another for a specialized shell) and let CShells pick the right one based on the active shell.
- **Feature composition per shell**  Configure each shell with a set of feature identifiers; features declare dependencies on other features, and CShells orders them so that shared building blocks (like `Core` features) are initialized before dependent features.
- **Plays nicely with minimal APIs and common middleware**  CShells integrates with the standard ASP.NET Core pipeline, including minimal APIs, HTTPS redirection, routing, and Swagger/OpenAPI.

## Configuration Details

CShells supports configuring shells and their enabled features via the application's configuration (for example, `appsettings.json`). The default configuration section name is `CShells`.

At a high level:
- The `CShells` section is bound to `CShells.Configuration.CShellsOptions`.
- The options are converted to runtime `ShellSettings` using `CShells.Configuration.ShellSettingsFactory.CreateFromOptions`.
- The `AddCShells(configuration)` extension method wraps this process and registers `IShellHost` (implemented by `DefaultShellHost`) and the configured shells.

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

The `samples/CShells.SampleApp` project demonstrates a complete CShells integration:

```bash
cd samples/CShells.SampleApp
dotnet run
```

Then access:
- `http://localhost:5000/` - Default shell (Weather feature)
- `http://localhost:5000/admin` - Admin shell (Admin feature)

## License

MIT License - see [LICENSE](LICENSE) for details.
