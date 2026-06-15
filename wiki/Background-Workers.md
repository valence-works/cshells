# Background Workers

CShells exposes active shell generations through `IShellRegistry`. Use `IShell.BeginScope()` in background services, scheduled jobs, or any non-HTTP workload that needs access to shell-scoped services.

---

## `IShell.BeginScope()`

`IShell.BeginScope()` creates a tracked `IShellScope` wrapping a shell's `IServiceProvider`.

```csharp
public interface IShell
{
    IShellScope BeginScope();
}
```

The scope is `IAsyncDisposable`. Always dispose it when you're done to release scoped resources and decrement the shell's active-scope counter.

---

## Iterating All Shells

The most common pattern is iterating over all shells and performing work for each one:

```csharp
using CShells;
using CShells.Lifecycle;
using Microsoft.Extensions.Hosting;

public class DataSyncWorker(
    IShellRegistry registry,
    ILogger<DataSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var shell in registry.GetActiveShells())
            {
                await using var scope = shell.BeginScope();

                var syncService = scope.ServiceProvider.GetService<IDataSyncService>();
                if (syncService is not null)
                    await syncService.SyncAsync(stoppingToken);
                else
                    logger.LogDebug("Shell '{Shell}' does not have IDataSyncService", shell.Descriptor);
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

Register the worker:

```csharp
builder.Services.AddHostedService<DataSyncWorker>();
```

---

## Working with a Specific Shell

If you need to target one specific shell:

```csharp
using CShells.Lifecycle;

public class TenantReportGenerator(IShellRegistry registry)
{
    public async Task GenerateReportAsync(string tenantId, CancellationToken ct)
    {
        var shell = await registry.GetOrActivateAsync(tenantId, ct);

        await using var scope = shell.BeginScope();

        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
        await reportService.GenerateAsync(ct);
    }
}
```

---

## Skipping Shells Without a Required Service

A shell may or may not have a particular feature enabled. Use `GetService<T>()` (nullable) instead of `GetRequiredService<T>()` when a service is optional:

```csharp
foreach (var shell in registry.GetActiveShells())
{
    await using var scope = shell.BeginScope();

    var processor = scope.ServiceProvider.GetService<IQueueProcessor>();
    if (processor is null)
        continue;  // This shell doesn't have the queue processing feature

    await processor.ProcessPendingAsync(stoppingToken);
}
```

---

## Registering Background Workers as Shell Features

You can register a background worker from within a feature so it is automatically active for shells that enable the feature:

```csharp
[ShellFeature("Notifications", DependsOn = ["Core"])]
public class NotificationsFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<INotificationSender, EmailNotificationSender>();
        // The worker is registered in the root DI container (not shell-scoped)
        // and uses IShellRegistry + IShell.BeginScope() to access shell services
    }
}
```

Then register the worker once at the application level:

```csharp
builder.Services.AddHostedService<NotificationDispatchWorker>();
```

---

## Tips

- **Always dispose scopes** — `IShellScope` is `IAsyncDisposable`. Use `await using`.
- **Use `GetService<T>()` for optional services** — not all shells will have all features enabled.
- **Re-query `IShellRegistry.GetActiveShells()` on each iteration** — the shell list can change at runtime when shells are added, removed, or reloaded.
- **Access `IShellRegistry` via injection, not closure** — always use the injected `IShellRegistry` reference; do not capture it in a static variable.
