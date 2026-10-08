# Quickstart: CShells.Nuplane

## Compose after Nuplane autoload

Install the optional `CShells.Nuplane` package and configure Nuplane package loading before adding the CShells observer bridge:

```csharp
using CShells.DependencyInjection;
using CShells.Nuplane;
using Microsoft.Extensions.DependencyInjection;
using Nuplane;
using Nuplane.Loading.Hosting.Builder;

IServiceCollection services = new ServiceCollection();
services.AddNuplane(nuplane => nuplane.AutoloadPackages());

var shells = services.AddCShells();
shells.WithNuplaneFeatureDiscovery();
```

The adapter appends its observer after the package auto-loader and previously registered observers. Do not register the coordinator with `OnPackagesChanged<T>()`; the adapter creates one coordinator and aliases it to both the observer and build-participant contracts.

`AddNuplane` takes a configuration callback and returns the service collection; configure autoload and existing Nuplane observers inside that callback. Add `WithNuplaneFeatureDiscovery` only after it returns, so the coordinator observer is appended after `AutoloadPackages()`.

The generic defaults are observer integration enabled, refresh on changed or pending work, and automatic reload disabled. The provider is selected only because the host called `WithNuplaneFeatureDiscovery`. To combine it with host or explicit assemblies, add the corresponding CShells provider composition explicitly.

## Preserve a host's refresh/reload profile

Foundation.Host's profile is every eligible completion plus automatic reload:

```csharp
shells.WithNuplaneFeatureDiscovery(options =>
{
    options.RefreshTrigger = NuplaneRefreshTrigger.EveryEligibleCompletion;
    options.AutoReload = true;
});
```

Workbench's profile is changed-or-pending refresh and no automatic reload:

```csharp
shells.WithNuplaneFeatureDiscovery(options =>
{
    options.RefreshTrigger = NuplaneRefreshTrigger.ChangedOrPending;
    options.AutoReload = false;
});
```

Reload-off still refreshes the catalog. Setting `Enabled = false` suppresses observer-driven refresh and reload while leaving the provider's ordinary initial query available.

## Report host-specific reload failures

`ReloadActiveAsync` returns partial per-shell outcomes in `ReloadResult.Error`. A host may configure `OnReloadResults` through the standard options pipeline so its own logging and refusal adapter receives the raw results:

```csharp
using Microsoft.Extensions.Logging;

services.AddOptions<NuplaneIntegrationOptions>()
    .Configure<ILoggerFactory>((options, loggerFactory) =>
    {
        var logger = loggerFactory.CreateLogger("ShellReloadResults");
        options.OnReloadResults = (results, _) =>
        {
            foreach (var result in results.Where(result => result.Error is not null))
            {
                // Apply host-owned refusal recognition and message formatting here.
                logger.LogError(result.Error, "Reloading shell {Shell} failed.", result.Name);
            }
            return ValueTask.CompletedTask;
        };
    });
```

The adapter inspects returned errors independently of the callback. It keeps failed reload work pending for a later eligible completion. A callback failure also keeps pending work; already committed reloads are not undone.

## Deferred freshness

When a package completion arrives and no shell is active, the integration retains the required refresh epoch and returns without scanning assemblies or activating a shell. A later requested shell build refreshes through `IRuntimeFeatureCatalog` before CShells initializes or reads the feature catalog. A failed or cancelled refresh remains pending. Events during refresh remain outstanding beyond the epoch that refresh captured.

This participant lease is intentionally a no-op after freshness work. It does not protect a package load context or promise assembly readability through a shell generation. Package retirement and unload stay with their owning host/runtime layers.
