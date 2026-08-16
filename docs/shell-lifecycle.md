# Shell Lifecycle

CShells builds one isolated service provider per shell generation. Lifecycle APIs let features run startup work after the provider is built and cooperative shutdown work while an old generation drains.

## Runtime States

A shell generation moves through:

```text
Initializing -> Active -> Deactivating -> Draining -> Drained -> Disposed
```

`IShellInitializer` instances run while the shell is `Initializing`, before the generation is published as `Active`. `IDrainHandler` instances run while the shell is `Draining`, after outstanding `IShellScope` handles finish or the drain deadline is reached. `IShellTerminator` instances run while the shell is `Drained`, after all drain handlers complete and before the shell's service provider is disposed.

Dynamic shell endpoint generations are prepared as complete candidates. Feature mapping,
middleware composition, and route validation finish before the candidate replaces the published
endpoint snapshot, so a failed mapping or collision leaves the previous generation available and a
successful replacement never exposes an empty routing state. Collision diagnostics include both
route owners and compare normalized parameter templates plus the full HTTP method sets.

Requests that have already matched an endpoint remain bound to the shell identifier and exact
generation in that endpoint's metadata. The old generation is removed from routing when draining
begins, while its middleware pipeline and scoped provider remain available until in-flight scopes
finish.

## Initializer Ordering

Feature dependencies and initializer order are separate concepts:

- `[ShellFeature(DependsOn = ...)]` still means "configure the dependency first".
- Lifecycle ordering controls when `IShellInitializer` instances execute after the shell provider has been built.
- Existing unordered `IShellInitializer` registrations remain valid and run in `LifecyclePhase.Default`.
- Unordered initializers keep DI registration order unless explicit lifecycle metadata is used.

Use `AddShellInitializer<TInitializer>()` for first-class lifecycle metadata:

```csharp
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

[ShellFeature("StorageProvider")]
public sealed class StorageProviderFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddShellInitializer<ApplyStorageMigrations>(
            LifecyclePhase.Prepare,
            order: 100);
    }
}

[ShellFeature("Runtime")]
public sealed class RuntimeFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddShellInitializer<StartRuntime>(
            LifecyclePhase.Start,
            order: 100);
    }
}
```

Execution order is deterministic:

1. `LifecyclePhase.Prepare`
2. `LifecyclePhase.Default`
3. `LifecyclePhase.Start`

Within a phase, lower numeric `order` runs first. Equal phase/order ties use DI registration order as a deterministic tie-break and are reported as non-fatal diagnostics.

## Compatibility

Existing registrations continue to work:

```csharp
public sealed class ExistingFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IShellInitializer, FirstInitializer>();
        services.AddTransient<IShellInitializer, SecondInitializer>();
    }
}
```

Both initializers run in `LifecyclePhase.Default`, and `FirstInitializer` still runs before `SecondInitializer`.

`AddShellInitializer<TInitializer>()` registers `TInitializer` as transient and also registers `IShellInitializer` through the shell service provider. Initializers may depend on shell-scoped services, but feature constructors should still only consume root-level services plus supported shell context values.

## Attribute Metadata

When a legacy `IShellInitializer` registration should carry lifecycle metadata without changing the registration call, apply `LifecycleOrderAttribute` to the initializer type:

```csharp
[LifecycleOrder(LifecyclePhase.Prepare, 50)]
public sealed class ApplySchemaInitializer(IMigrationRunner migrations) : IShellInitializer
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        migrations.ApplyAsync(cancellationToken);
}
```

Explicit metadata from `AddShellInitializer<TInitializer>(...)` overrides attribute metadata for the same initializer type.

## Provider/Base Feature Pairs

Provider features should keep depending on base features for service configuration, then use lifecycle phases to run provider preparation before base runtime startup.

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

Quartz configures first because `QuartzPostgreSqlFeature` depends on `QuartzFeature`. PostgreSQL migrations run first because they are in `Prepare`; the scheduler starts later in `Start`.

## Terminator Ordering

`IShellTerminator` is the teardown counterpart to `IShellInitializer`: ordered, sequential shutdown work that runs during graceful drain, after the shell reaches `Drained` and before its service provider is disposed. **The shell container is fully usable during termination** — terminators are resolved from a fresh scope of the shell's provider and may resolve any shell service. This is the home for teardown that must coordinate across services (flush A before B, stop a task that drains another singleton), which cannot be expressed in singleton `Dispose`/`DisposeAsync` because MS DI disposes singletons in reverse realization order.

Register terminators with `AddShellTerminator<TTerminator>()`, typically paired with the matching initializer at the same phase and order:

```csharp
[ShellFeature("Runtime")]
public sealed class RuntimeFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddShellInitializer<StartRuntime>(LifecyclePhase.Start, order: 100);
        services.AddShellTerminator<StopRuntime>(LifecyclePhase.Start, order: 100);
    }
}
```

Terminators reuse `LifecyclePhase` but execute **mirror-reversed**:

1. `LifecyclePhase.Start`
2. `LifecyclePhase.Default`
3. `LifecyclePhase.Prepare`

Within a phase, higher numeric `order` runs first, and equal phase/order ties run in reverse DI registration order (reported as non-fatal diagnostics). A terminator registered with the same phase/order as an initializer therefore tears down at the mirrored point — whatever started last stops first:

| Initializer (startup order)          | Terminator (teardown order)          |
| ------------------------------------ | ------------------------------------ |
| 1. `ApplyMigrations` (Prepare, 100)  | 1. `StopRuntime` (Start, 100)        |
| 2. `WarmCache` (Default, 0)          | 2. `FlushCache` (Default, 0)         |
| 3. `StartRuntime` (Start, 100)       | 3. `ReleaseStorage` (Prepare, 100)   |

Semantics:

- **Sequential, log-and-continue.** Terminators run one at a time (unlike parallel drain handlers). A terminator that throws is logged and recorded in `DrainResult.TerminatorResults`; the remaining terminators still run. Terminator failures never change the `DrainStatus`.
- **Cancellation budget.** Terminators share one cancellation token: the remaining drain deadline, never less than the grace period (scope-wait and drain handlers may have consumed the deadline). After a force-drain, terminators get one fresh grace-period-bounded chance instead of an already-cancelled token. Under an unbounded policy termination is unbounded, interruptible by `ForceAsync`. A terminator that ignores its token is abandoned after cancellation plus the grace period; disposal proceeds.
- **Graceful drain only.** Terminators run on every path that drains (host shutdown, `ReloadAsync`, `UnregisterBlueprintAsync`, force-drain). They do **not** run on the emergency-dispose path taken when the host's shutdown timeout is breached — terminators are a graceful-drain facility, not a guaranteed destructor. Singleton `Dispose`/`DisposeAsync` remains the last-resort cleanup.
- **Compatibility.** Legacy `services.AddTransient<IShellTerminator, X>()` registrations run in `LifecyclePhase.Default`, in reverse DI registration order. `LifecycleOrderAttribute` applies to terminator types the same way it does to initializers.

## Drain

Drain behavior is intentionally unchanged by initializer ordering. `IDrainHandler` implementations are resolved from the shell provider and invoked in parallel during `Draining` — they are the cooperative "let in-flight work finish" hook. Ordered teardown is provided by `IShellTerminator` (see [Terminator Ordering](#terminator-ordering)), which runs after all drain handlers complete. Per-terminator outcomes are reported in `DrainResult.TerminatorResults` alongside `HandlerResults`.

Existing deadline, force-drain, grace-period, and result-reporting behavior remains the compatibility baseline.

## Diagnostics

CShells fails activation before initializer side effects when explicit lifecycle metadata references an initializer type that is not resolved from DI or does not implement `IShellInitializer`. Exception messages include the shell descriptor and affected initializer type names.

Equal phase/order ties are allowed for compatibility and deterministic execution, but they are surfaced as diagnostics so authors can choose clearer order values when desired.
