# CShells

A modular multi-tenancy framework for .NET that enables building feature-based applications with isolated services, configuration, and background workers.

## Purpose

CShells is the core runtime package that provides shell hosting, feature discovery, dependency injection container management, and configuration-driven multi-tenancy.

## Key Features

- **Multi-shell architecture** - Each shell has its own isolated DI container
- **Feature-based modularity** - Features are discovered automatically via attributes
- **Dependency resolution** - Features can depend on other features with topological ordering
- **Configuration-driven** - Shells and their features are configured via appsettings.json or code
- **Background worker support** - Execute work in shell contexts via `IShellContextScopeFactory`
- **Runtime shell management** - Add, update, or remove shells at runtime

## Installation

```bash
dotnet add package CShells
```

## Quick Start

### 1. Create a Feature

```csharp
using CShells.Features;
using Microsoft.Extensions.DependencyInjection;

[ShellFeature("Core", DisplayName = "Core Services")]
public class CoreFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ITimeService, TimeService>();
    }
}
```

### 2. Configure Shells

```json
{
  "CShells": {
    "Shells": [
      {
        "Name": "Default",
        "Features": [ "Core" ]
      }
    ]
  }
}
```

### 3. Register CShells

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddCShells();

var app = builder.Build();
app.Run();
```

## Shell Context Scopes & Background Work

Use `IShellContextScopeFactory` to create scoped services within a shell's service provider:

```csharp
public class ShellBackgroundWorker : BackgroundService
{
    private readonly IShellHost _shellHost;
    private readonly IShellContextScopeFactory _scopeFactory;

    public ShellBackgroundWorker(
        IShellHost shellHost,
        IShellContextScopeFactory scopeFactory)
    {
        _shellHost = shellHost;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var shell in _shellHost.AllShells)
            {
                using var scope = _scopeFactory.CreateScope(shell);
                var service = scope.ServiceProvider.GetService<IMyService>();
                service?.Execute();
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

## Configuration Options

### Code-first Configuration

```csharp
builder.Services.AddCShells(cshells =>
{
    cshells.AddShell("Default", shell => shell
        .WithFeatures("Core", "Weather")
        .WithConfiguration("Theme", "Dark")
        .WithConfiguration("MaxItems", "100"));
});
```

### Custom Shell Settings Provider

```csharp
public class DatabaseShellSettingsProvider : IShellSettingsProvider
{
    public async Task<IEnumerable<ShellSettings>> GetAllAsync()
    {
        // Load from database, API, etc.
        return Enumerable.Empty<ShellSettings>();
    }
}

builder.Services.AddCShells(cshells =>
{
    cshells.WithProvider<DatabaseShellSettingsProvider>();
});
```

## Learn More

- [Main Documentation](https://github.com/sfmskywalker/cshells)
- [ASP.NET Core Integration](../CShells.AspNetCore)
- [FluentStorage Provider](../CShells.Providers.FluentStorage)
