# Shell Blueprint Providers

CShells uses exactly one `IShellBlueprintProvider` per host. Code-first `AddShell(...)` registrations use the built-in in-memory provider. External sources register their provider through `AddBlueprintProvider(...)` or a provider-specific extension such as `WithConfigurationProvider(...)` or `WithFluentStorageBlueprints(...)`.

If you need to combine several sources, implement one custom provider that performs the fan-out internally and register only that provider. The runtime intentionally avoids implicit multi-provider merging so lookup, paging, and ownership are deterministic.

---

## Provider Registration Methods

### `AddShell` - Code-First

```csharp
cshells.AddShell("Tenant1", shell => shell
    .WithFeatures("Core", "Premium")
    .WithConfiguration("WebRouting:Path", "tenant1"));
```

Code-first shells are read-only blueprints backed by the built-in in-memory provider.

### `WithConfigurationProvider` - `appsettings.json`

```csharp
cshells.WithConfigurationProvider(builder.Configuration);
// or with a custom section name:
cshells.WithConfigurationProvider(builder.Configuration, "TenantConfig");
```

Configuration-backed blueprints re-read their configuration when composed for activation or reload.

### `AddBlueprintProvider` - Custom Provider

```csharp
builder.AddShells(cshells =>
{
    cshells.AddBlueprintProvider(sp =>
        sp.GetRequiredService<DatabaseShellBlueprintProvider>());
});
```

The provider type is resolved from DI, so it can receive constructor dependencies.

---

## Built-in Provider Types

| Type | Description |
|---|---|
| `InMemoryShellBlueprintProvider` | Read-only in-memory blueprints used by code-first `AddShell(...)` registrations |
| `ConfigurationShellBlueprintProvider` | Reads blueprints from `IConfiguration` |
| `FluentStorageShellBlueprintProvider` | Reads JSON blueprints from disk or cloud storage and supports mutation |

---

## Mutable Sources

Providers wrapping mutable stores can attach an `IShellBlueprintManager` to each returned `ProvidedBlueprint`. The manager persists create/update/delete operations. Runtime activation and reload remain explicit registry operations:

```csharp
var manager = await registry.GetManagerAsync("Tenant1");
if (manager is null)
    throw new InvalidOperationException("The shell source is read-only or unknown.");

await manager.UpdateAsync(updatedSettings);
await registry.ReloadAsync("Tenant1");
```

Use `IShellRegistry.UnregisterBlueprintAsync(name)` for removal so CShells can delete the blueprint through its manager and then drain the active shell generation.

---

## Best Practices

- Register exactly one blueprint source per host.
- Use a custom `IShellBlueprintProvider` when several backing stores need to appear as one catalogue.
- Use `IShellRegistry.GetOrActivateAsync(name)` to activate a shell on demand.
- Use `IShellRegistry.ReloadAsync(name)` or `ReloadActiveAsync()` after external source changes.
- Ensure custom providers are thread-safe; they may be called concurrently.
