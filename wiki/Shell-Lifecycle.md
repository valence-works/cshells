# Shell Lifecycle

CShells builds one isolated service provider per shell generation. Lifecycle APIs let features run startup work after the provider is built and cooperative shutdown work while an old generation drains.

## Initializer Ordering

Feature dependencies and initializer order are intentionally separate:

- `[ShellFeature(DependsOn = ...)]` configures dependencies first.
- `IShellInitializer` lifecycle metadata controls startup execution after the shell provider is built.
- Existing direct `IShellInitializer` registrations remain valid and run in `LifecyclePhase.Default`.
- Existing unordered registrations keep DI registration order.

Use `AddShellInitializer<TInitializer>()` when startup side effects need a deterministic phase:

```csharp
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

Execution order is `Prepare`, then `Default`, then `Start`. Within a phase, lower numeric order runs first; equal phase/order ties use DI registration order as the deterministic tie-break.

## Compatibility

Legacy registrations continue to work:

```csharp
services.AddTransient<IShellInitializer, FirstInitializer>();
services.AddTransient<IShellInitializer, SecondInitializer>();
```

Both run in `LifecyclePhase.Default`, with `FirstInitializer` before `SecondInitializer`.

`AddShellInitializer<TInitializer>()` registers `TInitializer` as transient unless you have already registered it yourself (an explicit `services.AddSingleton<TInitializer>()` is preserved, and the lifecycle then initializes that very instance), and resolves it from the shell service provider at activation time, so initializers may depend on shell-scoped services.

## Provider/Base Feature Pairs

Provider features should keep depending on the base feature for service configuration, then use lifecycle phases to run provider preparation before base startup.

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

Quartz services configure first because the provider depends on the base feature. PostgreSQL migrations initialize first because they are in `Prepare`; the scheduler starts later in `Start`.

## Drain

`IDrainHandler` behavior is unchanged. Handlers are resolved from the shell provider and invoked in parallel during `Draining`.

Ordered or phased drain execution is deferred to a future design. Existing deadline, force-drain, grace-period, and result-reporting behavior remains the compatibility baseline.

## Diagnostics

Invalid explicit lifecycle metadata fails activation before initializer side effects. Exception messages include the shell descriptor and affected initializer type names.

Equal phase/order ties are allowed for compatibility and deterministic execution, but CShells emits non-fatal diagnostics so authors can choose clearer order values when desired.
