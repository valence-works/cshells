# Integration Patterns

This guide explains how to integrate CShells into existing ASP.NET Core applications without route conflicts.

## Overview

CShells registers shell-specific endpoints dynamically via `IWebShellFeature.MapEndpoints`. When integrating into an existing app, you need to ensure shell routes don't collide with host routes.

## Pattern 1: Dedicated Path Prefixes (Recommended)

Give each shell a unique path prefix:

```json
{
  "CShells": {
    "Shells": {
      "Acme": { "Configuration": { "WebRouting": { "Path": "tenants/acme" } }  },
      "Contoso": { "Configuration": { "WebRouting": { "Path": "tenants/contoso" } }  }
    }
  }
}
```

Result:

- Shell routes: `/tenants/acme/*`, `/tenants/contoso/*`
- Host routes: `/api/*`, `/admin/*`, `/`
- No conflicts.

## Pattern 2: Subdomain-Based Isolation

```json
{
  "CShells": {
    "Shells": {
      "Acme": { "Configuration": { "WebRouting": { "Host": "acme.example.com" } }  }
    }
  }
}
```

Result: `acme.example.com` routes to the Acme shell; `www.example.com` routes to the host app.

## Pattern 3: Mixed Host + Shell Routes

```csharp
var app = builder.Build();

// Host routes first
app.MapGet("/", () => "Host home page");
app.MapControllers();

// Shell routes second
app.MapShells();
```

Register host routes **before** `MapShells()` to avoid shadowing.

## Pattern 4: Root-Level Shell

Set `Path` to `""` when the **entire** application is multi-tenant:

```json
{
  "CShells": {
    "Shells": {
      "Default": {
        "Configuration": { "WebRouting": { "Path": "" } }
      }
    }
  }
}
```

> **Warning:** shell routes will match all root-level requests. Only use this when you have no host-specific routes.

## Excluding Paths from Shell Resolution

```csharp
builder.Services.AddCShells(shells =>
{
    shells.WithWebRouting(options =>
    {
        options.ExcludePaths = ["/health", "/swagger", "/admin"];
    });
});
```

Requests to excluded prefixes bypass shell resolution entirely.

## Middleware Ordering

```csharp
var app = builder.Build();

app.UseExceptionHandler("/Error");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Host routes
app.MapControllers();
app.MapHealthChecks("/health");

// Shell routes
app.MapShells();
```

## Complete Example

```csharp
using CShells.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.AddShells(cshells =>
{
    cshells.WithConfigurationProvider(builder.Configuration);
    cshells.WithAssemblies(typeof(MyFeature).Assembly);
    cshells.WithSharedAssemblies("Elsa", "Elsa.*");
    cshells.WithSharedAssembliesWhere(name =>
        name.StartsWith("MyFramework.", StringComparison.OrdinalIgnoreCase));
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

// Host routes
app.MapGet("/", () => "Host home");
app.MapControllers();
app.MapHealthChecks("/health");

// Shell routes (resolved by path prefix, host, or header)
app.MapShells();

app.Run();
```

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `AmbiguousMatchException` | Multiple shells share the same route | Give each shell a unique `WebRouting:Path` |
| Host routes return 404 | A root-level shell (`Path: ""`) shadows them | Add path exclusions or move shells under a prefix |
| Shell routes return 404 | `MapShells()` not called, or features not discovered | Verify `MapShells()` is in the pipeline and features are scanned |

## Shared Assembly Selectors

Use root `CShells:SharedAssemblies` configuration or code-first builder calls when an integration package needs to share a framework family:

```json
{
  "CShells": {
    "SharedAssemblies": [
      "Elsa",
      "Elsa.*"
    ]
  }
}
```

`Elsa` is an exact assembly simple-name match. `Elsa.*` is a prefix match against simple names only. The wildcard is only valid as the final character; suffix or contains-style patterns such as `*.Contracts` are intentionally invalid.

When no explicit assembly providers are configured, shared assembly selectors narrow the default host-derived discovery set. When explicit providers are configured with `WithAssemblies(...)`, `WithHostAssemblies()`, or `WithAssemblyProvider(...)`, matching host-derived shared assemblies are added to those provider results and deduplicated.

Prefer narrow patterns for framework contracts and common infrastructure. Avoid broad patterns that include shell-specific implementation assemblies, because sharing those assemblies can weaken shell isolation.

## Provider/Base Feature Lifecycle Ordering

Provider features often depend on a base feature for service configuration, but need provider-specific preparation to run before the base runtime starts. Keep `DependsOn` pointed at the base feature, then use initializer phases for lifecycle work.

```csharp
[ShellFeature("Quartz")]
public sealed class QuartzFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddShellInitializer<StartQuartzScheduler>(
            LifecyclePhase.Start,
            order: 100);
    }
}

[ShellFeature("QuartzPostgreSql", DependsOn = [typeof(QuartzFeature)])]
public sealed class QuartzPostgreSqlFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddShellInitializer<RunQuartzPostgreSqlMigrations>(
            LifecyclePhase.Prepare,
            order: 100);
    }
}
```

In this pattern, `QuartzFeature.ConfigureServices` still runs before `QuartzPostgreSqlFeature.ConfigureServices`, so base services are available for provider configuration. During activation, PostgreSQL migrations run in `Prepare` before the Quartz scheduler starts in `Start`.

## Reading the Runtime Feature Catalog

External integrations (for example, `Elsa.Modularity.Nuplane`) sometimes need to refresh and read the set of discovered features at runtime. The **supported** way to do this is the public `IRuntimeFeatureCatalog` contract — resolve it from the application's root service provider. Do **not** reflect over the internal `CShells.Features.RuntimeFeatureCatalog` type; that type is an implementation detail and is not a stable surface.

`AddCShells()` registers `IRuntimeFeatureCatalog` as a singleton automatically.

```csharp
using CShells.Features;

public sealed class FeatureCatalogReader(IRuntimeFeatureCatalog catalog)
{
    public async Task<int> CountFeaturesAsync(CancellationToken cancellationToken)
    {
        // Re-evaluate feature sources and commit a fresh snapshot.
        var snapshot = await catalog.RefreshAsync(cancellationToken);

        foreach (var feature in snapshot.FeatureDescriptors)
        {
            // Stable, typed fields — no reflection required.
            Console.WriteLine($"{feature.Id} ({feature.DisplayName}): {feature.Description}");
        }

        return snapshot.FeatureDescriptors.Count;
    }
}
```

The contract exposes only what external consumers need:

| Member | Purpose |
|--------|---------|
| `IRuntimeFeatureCatalog.RefreshAsync(ct)` | Re-evaluates feature sources and returns a new snapshot. |
| `IRuntimeFeatureCatalog.EnsureInitializedAsync(ct)` | Performs an initial refresh only if the catalog has never been built. |
| `IRuntimeFeatureCatalog.CurrentSnapshot` | The most recently committed snapshot. |
| `IRuntimeFeatureCatalogSnapshot.FeatureDescriptors` | The discovered features (use `.Count` for the descriptor count). |
| `IRuntimeFeatureCatalogSnapshot.Generation` / `RefreshedAt` | Monotonic generation number and UTC timestamp of the snapshot. |
| `RuntimeFeatureDescriptor.Id` / `Name` | The feature identifier (`Name` is an alias for `Id`). |
| `RuntimeFeatureDescriptor.DisplayName` | Human-readable name; falls back to `Id` when none is declared. |
| `RuntimeFeatureDescriptor.Description` | Optional description, or `null`. |
| `RuntimeFeatureDescriptor.Dependencies` | Names of the features this feature depends on. |

### Migration from reflection

Consumers that previously reflected over `RuntimeFeatureCatalog.RefreshAsync(...)` and read `FeatureDescriptors`/`FeatureName`/`DisplayName`/`Description` should switch to resolving `IRuntimeFeatureCatalog` and using `RuntimeFeatureDescriptor`'s typed members. The internal `RuntimeFeatureCatalog` type still exists for backward compatibility, but the public contract is the only supported integration surface going forward.

## Best Practices

- Use dedicated path prefixes (`/tenants/*`, `/apps/*`) for shells
- Register host routes **before** `MapShells()`
- Use `ExcludePaths` to protect health checks, Swagger, and admin routes
- Monitor logs for path-conflict warnings
- Read features through the public `IRuntimeFeatureCatalog` contract instead of reflecting over internals
