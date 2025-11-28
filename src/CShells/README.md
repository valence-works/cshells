# CShells Configuration

CShells supports configuring shells and their enabled features via the application's configuration (appsettings.json). The default configuration section name is `CShells`.

Example appsettings.json (minimal):

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

Binding and registration
- Use Microsoft.Extensions.Configuration to bind the `CShells` section to `CShells.Configuration.CShellsOptions`.
- Convert the bound DTOs into runtime `ShellSettings` using `CShells.Configuration.ShellSettingsFactory.CreateFromOptions`.
- For convenience, call `services.AddCShells(configuration)` to bind the configuration and register `IShellHost` (DefaultShellHost) and the configured shells.

Notes
- Shell names are case-insensitive and must be unique. The factory will throw an exception for duplicate names.
- Feature names are trimmed and preserved in the configured order.

## Shell Context Scopes

Shell context scopes provide a way to create scoped services within a shell's service provider. This is particularly useful for background workers and other services that need to execute work in the context of a specific shell.

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
