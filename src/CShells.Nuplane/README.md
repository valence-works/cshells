# CShells.Nuplane

`CShells.Nuplane` is an optional adapter that lets CShells discover features from assemblies Nuplane has already loaded for active packages. Add it only to hosts that compose Nuplane package loading and CShells.

## Compose after Nuplane loading

Configure Nuplane autoload and any existing package observers first. Then explicitly select the adapter from the CShells builder:

```csharp
using CShells.DependencyInjection;
using CShells.Nuplane;
using Microsoft.Extensions.DependencyInjection;
using Nuplane;
using Nuplane.Loading.Hosting.Builder;

IServiceCollection services = new ServiceCollection();
services.AddNuplane(nuplane =>
{
    nuplane.AutoloadPackages();
    // Add existing Nuplane observers here.
});

var shells = services.AddCShells();
shells.WithNuplaneFeatureDiscovery();
```

`AddNuplane` accepts a configuration callback and returns `IServiceCollection`. Calling `WithNuplaneFeatureDiscovery` afterward appends the CShells adapter observer after Nuplane's autoloader and existing observer registrations. The adapter uses one root coordinator for both Nuplane observation and CShells build participation. Do not register another coordinator through `OnPackagesChanged<T>()`.

The provider queries `IPackageAssemblyCatalog` and contributes each `PackageAssemblies.Assemblies` collection in package order. These are already-loaded `Assembly` instances. The adapter does not read `AssemblyReferences`, load packages, or activate them. Adding the package alone does not replace CShells' current feature sources; the host opts in by calling `WithNuplaneFeatureDiscovery`.

## Choose refresh and reload behavior

Generic defaults keep observer work enabled, refresh on changes or retained freshness, and leave automatic reload off:

```csharp
shells.WithNuplaneFeatureDiscovery(options =>
{
    options.Enabled = true;
    options.RefreshTrigger = NuplaneRefreshTrigger.ChangedOrPending;
    options.AutoReload = false;
});
```

Configure host profiles independently. Foundation.Host can refresh and reload on every delivered eligible completion:

```csharp
shells.WithNuplaneFeatureDiscovery(options =>
{
    options.RefreshTrigger = NuplaneRefreshTrigger.EveryEligibleCompletion;
    options.AutoReload = true;
});
```

Workbench can refresh only for changes or pending freshness and leave active shells alone:

```csharp
shells.WithNuplaneFeatureDiscovery(options =>
{
    options.RefreshTrigger = NuplaneRefreshTrigger.ChangedOrPending;
    options.AutoReload = false;
});
```

An eligible completion is a delivered `OnPackagesReconciledAsync` callback with at least one successfully applied package or a committed removal. `ChangedOrPending` scans after additions, updates, removals, or a previous failed/deferred refresh. An unchanged completion with a fresh catalog and no pending reload does no work. `EveryEligibleCompletion` scans each eligible delivery, including unchanged successful applications.

`Enabled = false` suppresses new observer-driven refresh and reload requests. It does not disable the assembly provider's normal query when CShells initializes the feature catalog, and it does not discard freshness already pending for a later build. Reloading is independent: with `AutoReload = false`, a refresh updates catalog metadata while active shells keep their current generation. Disabling automatic reload pauses an already-pending failed reload; while disabled, new work does not create a reload request. A later eligible delivery with automatic reload enabled may retry retained failed reload work.

The adapter reads the current options once for each eligible reconciliation delivery and copies the four policy values before recording work. Standard `IOptionsMonitor` configuration therefore applies on the next eligible delivery, including values bound from a reloaded `IConfiguration`; changes do not start a background refresh or reload by themselves. The current delivery keeps the policy it captured even if configuration changes while its refresh is running. Build participation does not read options. Use `Configure`, `Bind`, or dependency-aware `AddOptions` configuration so the registered monitor owns the live values. A custom `IOptionsMonitor<NuplaneIntegrationOptions>` registration is supported; replacing only `IOptions<NuplaneIntegrationOptions>` is unsupported.

## Refresh cold shells before construction

When a package completion arrives with no active shell, the adapter records catalog freshness and returns without scanning assemblies, activating a shell, or reloading. The next requested shell build consumes pending freshness before CShells initializes or reads the feature catalog. A successful refresh is committed even if later shell initialization fails, so the next build can reuse that fresh snapshot.

Refresh requests and acknowledgements use separate epochs. A failed or cancelled refresh remains pending. A source change arriving during refresh remains outstanding after the in-flight refresh commits its captured epoch. The build participant returns a no-op lease and does not provide assembly-readability, load-context, or generation-lifetime protection.

## Report host-specific reload failures

`IShellRegistry.ReloadActiveAsync` returns one `ReloadResult` per active shell. The callback receives a stable read-only copy of those original results, including partial errors and nested exception chains. Use standard options configuration when reporting needs another service:

```csharp
using CShells.Nuplane;
using Microsoft.Extensions.Logging;

services.AddOptions<NuplaneIntegrationOptions>()
    .Configure<ILoggerFactory>((options, loggerFactory) =>
    {
        var logger = loggerFactory.CreateLogger("ShellReloadResults");
        options.OnReloadResults = (results, _) =>
        {
            foreach (var result in results.Where(result => result.Error is not null))
                logger.LogError(result.Error, "Reloading shell {Shell} failed.", result.Name);
            return ValueTask.CompletedTask;
        };
    });
```

The coordinator evaluates every `ReloadResult.Error` independently of the callback. Partial errors, thrown or cancelled registry calls, and callback failures retain reload work for a later eligible completion. A callback can report or translate host-specific refusals without replacing exception objects. A failure after one or more shells have already promoted does not roll those promotions back. The callback may request a manual shell build or reload because the refresh gate is released before automatic reload results are delivered; reentrant Nuplane observer dispatch is not part of this contract.

Package retirement and unload remain owned by the host/runtime layer that manages Nuplane packages.
