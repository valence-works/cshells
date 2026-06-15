# Runtime Shell Management

CShells supports activating, reloading, draining, unregistering, and inspecting shells at runtime without restarting the application. The runtime is centered on shell blueprints and active shell generations: blueprints describe how to compose `ShellSettings`, while `IShellRegistry` owns the currently active generations.

---

## `IShellRegistry`

`IShellRegistry` is registered automatically by `AddCShells()`. Inject it into services that need to activate, reload, drain, unregister, or inspect shells at runtime.

```csharp
using CShells.Lifecycle;

public class TenantRuntimeService
{
    private readonly IShellRegistry registry;

    public TenantRuntimeService(IShellRegistry registry)
    {
        this.registry = registry;
    }
}
```

---

## Creating or Updating a Blueprint

```csharp
public async Task CreateTenantAsync(string tenantId, string tier)
{
    var features = tier switch
    {
        "enterprise" => new[] { "Core", "Billing", "Reporting", "FraudDetection" },
        "pro"        => new[] { "Core", "Billing", "Reporting" },
        _            => new[] { "Core", "Billing" }
    };

    var settings = new ShellSettings(new ShellId(tenantId), features);
    settings.ConfigurationData["WebRouting:Path"] = tenantId;

    var manager = await registry.GetManagerAsync(tenantId)
        ?? throw new InvalidOperationException("The shell source is read-only or unknown.");

    await manager.CreateAsync(settings);
    await registry.GetOrActivateAsync(tenantId);
}
```

Mutable blueprint sources expose an `IShellBlueprintManager` for persisted create/update/delete operations. Read-only sources such as code-defined shells do not.

---

## Removing a Shell

```csharp
public async Task DeleteTenantAsync(string tenantId)
{
    await registry.UnregisterBlueprintAsync(tenantId);
}
```

Unregistering deletes the persisted blueprint through its owning manager, then drains and disposes any active generation.

---

## Updating a Shell

```csharp
public async Task UpgradeTenantAsync(string tenantId, string newTier)
{
    var features = newTier == "enterprise"
        ? new[] { "Core", "Billing", "Reporting", "FraudDetection" }
        : new[] { "Core", "Billing" };

    var settings = new ShellSettings(new ShellId(tenantId), features);
    settings.ConfigurationData["WebRouting:Path"] = tenantId;

    var manager = await registry.GetManagerAsync(tenantId)
        ?? throw new InvalidOperationException("The shell source is read-only or unknown.");

    await manager.UpdateAsync(settings);
    await registry.ReloadAsync(tenantId);
}
```

`UpdateAsync` only persists the blueprint. Call `ReloadAsync` when you want the running shell generation replaced.

---

## Reloading All Shells

```csharp
public async Task RefreshAllTenantsAsync()
{
    await registry.ReloadActiveAsync();
}
```

Full reloads only reload currently active shells. Inactive blueprints remain inactive until they are explicitly activated or requested.

---

## Reloading a Single Shell

```csharp
public async Task RefreshTenantAsync(string tenantId)
{
    await registry.ReloadAsync(tenantId);
}
```

- The registry composes fresh settings from the shell's blueprint.
- A successor generation is activated and promoted.
- The previous generation is cooperatively drained.
- Unrelated active shells are not affected.
- If the shell is unknown to the provider, the call throws without mutating runtime state.

---

## Inspecting Blueprints and Active Generations

Use the registry to inspect the blueprint catalogue and active generations.

```csharp
public class ShellStatusService(IShellRegistry registry)
{
    public IReadOnlyCollection<IShell> GetActiveShells() => registry.GetActiveShells();

    public Task<ShellPage> ListAsync(ShellListQuery query, CancellationToken ct) =>
        registry.ListAsync(query, ct);
}
```

`GetActive(name)` and `GetActiveShells()` read the in-memory active-generation index. `ListAsync()` returns a paginated catalogue view joined with current lifecycle state.

---

## Shell Lifecycle Notifications

CShells publishes notifications during shell lifecycle events.

### Available Notifications

| Notification | When |
|---|---|
| Lifecycle transition | Observe via `IShellLifecycleSubscriber` |
| Activation | New generation becomes active |
| Deactivation / drain | Old generation is being replaced, removed, or force-drained |
| Reload | `ReloadAsync` or `ReloadActiveAsync` builds successor generations |
| Unregister | Blueprint is deleted, then active runtime is drained and disposed |

### Reload Notification Ordering

During a **single-shell reload** (`ReloadAsync`):
1. The registry composes fresh settings from the shell's blueprint.
2. A new generation is built and initialized.
3. The new generation is promoted to active.
4. The previous generation enters cooperative drain.

During a **full active reload** (`ReloadActiveAsync`), the same sequence runs for each active shell. Inactive blueprints are not activated by this operation.

---

## `IShellRegistry` — Accessing Active Shells

Inject `IShellRegistry` to enumerate or look up active shell generations.

```csharp
using CShells.Lifecycle;

public class ShellDashboardService(IShellRegistry registry)
{
    public IEnumerable<string> GetActiveShellNames() =>
        registry.GetActiveShells().Select(shell => shell.Descriptor.Name);

    public IShell? GetShell(string name) =>
        registry.GetActive(name);
}
```

| Member | Description |
|---|---|
| `IShellRegistry.GetActiveShells()` | All currently active shell generations |
| `IShellRegistry.GetActive(name)` | The active generation for a shell name, or `null` |
| `IShellRegistry.GetOrActivateAsync(name)` | Returns the active generation or lazily activates it from its blueprint |
| `IShell.BeginScope()` | Opens a tracked DI scope for work inside that shell |
