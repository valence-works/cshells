# CShells

A modular multi-tenancy framework for .NET that enables building feature-based applications with isolated services, configuration, and background workers.

## Purpose

CShells is the core runtime package that provides blueprint-driven shell activation, cooperative drain-based reload, feature discovery, per-shell DI containers, and configuration-driven multi-tenancy.

## Key Features

- **Multi-shell architecture** — each shell has its own isolated DI container
- **Feature-based modularity** — features are discovered automatically via attributes
- **Dependency resolution** — features can depend on other features with topological ordering
- **Configuration-driven** — shells and their features are configured via `appsettings.json` or code
- **Generation lifecycle** — `Initializing → Active → Deactivating → Draining → Drained → Disposed`
- **Cooperative reload** — `IShellRegistry.ReloadAsync(name)` builds the next generation while draining the previous one; in-flight request scopes finish against the old provider
- **Observable events** — `IShellLifecycleSubscriber` receives every state transition
- **Configurable drain policies** — fixed, extensible, and unbounded timeouts

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
    "Shells": {
      "Default": {
        "Features": { "Core": {} }
      }
    }
  }
}
```

### 3. Register CShells

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddCShells(cshells =>
    cshells.WithConfigurationProvider(builder.Configuration));

var app = builder.Build();
app.Run();
```

## Shell Scopes & Background Work

`IShell.BeginScope()` returns a tracked `IShellScope` that (a) exposes a scoped `IServiceProvider` built from the shell's container and (b) delays drain-handler invocation while the scope is outstanding:

```csharp
public class ShellBackgroundWorker(IShellRegistry registry) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var name in registry.GetBlueprintNames())
            {
                var shell = registry.GetActive(name);
                if (shell is null) continue;

                await using var scope = shell.BeginScope();
                var service = scope.ServiceProvider.GetService<IMyService>();
                service?.Execute();
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

## Code-First Shell Registration

```csharp
builder.Services.AddCShells(cshells =>
{
    cshells.AddShell("Default", shell => shell
        .WithFeatures("Core", "Weather")
        .WithConfiguration("Theme", "Dark")
        .WithConfiguration("MaxItems", "100"));
});
```

## Per-Shell Initialization & Drain

Register `IShellInitializer` services for per-shell startup work and `IDrainHandler` services for cooperative shutdown:

```csharp
public class PaymentsFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPaymentProcessor, StripePaymentProcessor>();
        services.AddShellInitializer<RunPaymentMigrations>(
            LifecyclePhase.Prepare,
            order: 100);
        services.AddShellInitializer<StartPaymentProcessor>(
            LifecyclePhase.Start,
            order: 100);
        services.AddTransient<IDrainHandler, PaymentsDrainHandler>();
    }
}
```

Initializers run sequentially during `Initializing -> Active`. Existing direct `IShellInitializer` registrations still run in DI-registration order in `LifecyclePhase.Default`; `AddShellInitializer<T>()` adds explicit phase/order metadata and registers the initializer as transient unless you have already registered it yourself, in which case your lifetime is preserved. Drain handlers run in parallel during `Draining`, after all outstanding `IShellScope` handles have been released or the drain deadline elapses.

## Reload

```csharp
var result = await registry.ReloadAsync("payments");

// result.NewShell.Descriptor.Generation == previous + 1
// result.Drain (when non-null) is the cooperative drain on the previous generation.
if (result.Drain is not null)
    await result.Drain.WaitAsync();
```

## Learn More

- [Main Documentation](https://github.com/sfmskywalker/cshells)
- [ASP.NET Core Integration](../CShells.AspNetCore)
- [FluentStorage Provider](../CShells.Providers.FluentStorage)
