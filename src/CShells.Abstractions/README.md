# CShells.Abstractions

Core abstractions and interfaces for building shell features without dependencies on the full CShells framework.

## Purpose

This package contains the fundamental interfaces and models needed to build CShells features. By referencing only this package in your feature libraries, you avoid pulling in the entire CShells runtime and its dependencies.

## When to Use

- Building feature libraries that will be consumed by CShells applications
- Creating reusable features without coupling to the full framework
- Keeping feature library dependencies minimal

## Key Types

- `IShellFeature` - Base interface for defining features that register services
- `ShellSettings` - Configuration model for shell settings
- `IShellInitializer` - Startup hook resolved from the shell provider before a shell becomes active
- `LifecyclePhase`, `LifecycleOrderAttribute`, and `AddShellInitializer<T>()` - First-class initializer ordering APIs
- `IDrainHandler` - Cooperative drain hook invoked in parallel while an old shell generation shuts down
- Core abstractions for extensibility

## Installation

```bash
dotnet add package CShells.Abstractions
```

## Example Usage

```csharp
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

[ShellFeature("MyFeature")]
public class MyFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMyService, MyService>();
        services.AddShellInitializer<MyFeatureInitializer>(
            LifecyclePhase.Prepare,
            order: 100);
    }
}
```

`AddShellInitializer<T>()` registers the initializer as transient (unless you have already registered it yourself, in which case your lifetime is preserved) and attaches deterministic phase/order metadata. Existing direct `IShellInitializer` registrations remain valid and run in `LifecyclePhase.Default` using DI registration order.

## Learn More

- [Main Documentation](https://github.com/sfmskywalker/cshells)
- [CShells Package](../CShells) - Core runtime implementation
