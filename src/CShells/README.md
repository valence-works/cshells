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

## Activation Request Settlement

`GetOrActivateAsync` returns only after a generation's activation transaction has settled. `GetActive` and `GetAll` may expose a published candidate earlier so routing and lifecycle participants can resolve that exact generation while commit is in progress. A concurrent `GetOrActivateAsync` call waits for the same-name activation to commit or roll back; cancelling that wait does not cancel the activation. Activation participants must not await activation, reload, or unregister for the same shell name from their callbacks.

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

## Share Host-Owned Singleton Services

By default, CShells copies root service descriptors into each shell so singleton registrations have independent instances per shell. Opt in when every shell should use the host's singleton object and the host should retain its lifetime ownership:

```csharp
builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddCShells(cshells => cshells
    .ShareSingletonWithShells<IClock>()
    .AddShell("Default", shell => shell.WithFeatures("Core")));
```

The selection includes every unkeyed singleton registration for that service type, in registration order. The root provider resolves and owns those instances; shell providers borrow them without disposing them. The host disposes root-created disposable singletons with its normal provider lifecycle. Instances registered directly by the caller retain the usual caller-owned disposal behavior. Select aliases separately, and use the `Type` overload when the service type is chosen at runtime.

Keyed registrations are unchanged. A selection must have at least one unkeyed registration and every matching unkeyed registration must be singleton. Open generic selections, matching open-generic registrations, null factory results, and root-only exclusions fail when CShells builds a shell provider. Later shell core and feature registrations keep normal precedence, so this API does not force the host instance to win every single-service resolution.

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

Hosts that need to protect resources for a build or live generation can register root `IShellGenerationBuildParticipant` services. CShells assigns the immutable shell descriptor before blueprint composition, invokes participants after composition and name validation but before catalog initialization, then calls each acquired lease with the exact detailed snapshot used for feature selection before feature construction. Successful snapshot callbacks run in registration order. If one fails, callbacks after it are skipped while every acquired lease is unwound. Participants that fail before returning a lease clean up their own partial acquisition.

For a published shell, leases release in reverse order after the Disposed lifecycle notification and full provider teardown succeed. Failed builds and unpublished initializer candidates release after partial provider cleanup. A lifecycle/provider teardown failure retains unresolved leases in the root registry without retaining the shell or provider; individual release failures retain only the leases that failed. Distinct shell names may run callbacks concurrently. Callbacks must not start activation, reload, or unregister for the same name. This lifecycle hook manages protection ownership but does not perform package deletion or guarantee assembly unloading.

## Reload

```csharp
var result = await registry.ReloadAsync("payments");

// result.NewShell.Descriptor.Generation == previous + 1
// result.Drain (when non-null) is the cooperative drain on the previous generation.
if (result.Drain is not null)
    await result.Drain.WaitAsync();
```

## Runtime Feature Catalog Notifications

The public `IRuntimeFeatureCatalog` remains compatible with custom host implementations. When observing commits, capability-test the resolved catalog instance instead of resolving a separate event service:

```csharp
if (catalog is IRuntimeFeatureCatalogCommitSource commits)
    commits.SnapshotCommitted += snapshot => queueReconciliation(snapshot);
```

Subscribe before the first catalog initialization to receive its initial commit. Events are not replayed. If you subscribe later, subscribe first and read `CurrentSnapshot` to reconcile; a refresh may commit during that read, so compare generations to avoid missing or processing a generation twice. The property may also be newer than the exact snapshot in an event. Notifications are delivered in commit order outside the refresh lock, and subscriber exceptions are isolated. Handlers run synchronously, so enqueue lengthy work for later processing.

## Learn More

- [Main Documentation](https://github.com/sfmskywalker/cshells)
- [ASP.NET Core Integration](../CShells.AspNetCore)
- [FluentStorage Provider](../CShells.Providers.FluentStorage)
