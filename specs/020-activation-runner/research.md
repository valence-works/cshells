# Research: Opt-in Shell Activation Runner

## Existing activation behavior

- The default registry exposes `GetOrActivateAsync` and cancellation; it does not expose a generic startup run handle.
- `ShellRegistry.GetOrActivateAsync` now returns only a committed concrete `Shell` through its fast path. `CreateGenerationAsync` publishes a candidate before participant commit and sets `Shell.IsActivationCommitted` after completion callbacks and the final active-eligibility check.
- Lifecycle notification / `GetActive` is intentionally earlier than successful activation. The runner must verify committed marker and exact current identity after the registry call, and must not subscribe to lifecycle events.
- Existing `AddCShells` registers a startup hosted service, but this task must not add runner registration or invocation there. New opt-in runner registration can precede `AddCShells` because the singleton resolves `IShellRegistry` when the runner is resolved.
- Root descriptors are copied into shell providers through `ShellServiceExclusionRegistry`; `DefaultShellServiceExclusionProvider` already excludes root-only registry/participants and is the right place to exclude `IShellActivationRunner`.

Source reviewed: `src/CShells/Lifecycle/ShellRegistry.cs`, `src/CShells/Lifecycle/Shell.cs`, `src/CShells/DependencyInjection/ServiceCollectionExtensions.cs`, and `src/CShells/Hosting/DefaultShellServiceExclusionProvider.cs`.

## Retry/run ownership

One background coordinator executes initial attempts in order and then starts per-target retry workers only after it completes the `InitialPass` boundary. The coordinator awaits those retry workers, so Stop has one tracked join task and cannot lose a stubborn registry call. Per-target state is replaced under a short gate; all external calls happen outside that gate.

For external reconciliation, a known concrete `Shell` counts only when its committed marker is true and it is the exact current object. For unknown custom shell implementations, only the returned object can be validated against current identity after a successful custom registry call; the runner makes no external-state inference.

## Test time

The test package was absent from CShells package metadata but version 10.8.0 exists in the local NuGet cache. Its cached nuspec identifies Microsoft as author, pins source to dotnet/extensions commit `8f88b008401bd66a06240d1b7696cd89d921013b`, and shows empty net8.0/net9.0/net10.0 dependency groups. Use it only from `CShells.Tests`, pin it centrally, and verify a real restore. This is not a claim that 10.8.0 is latest.

## Non-goals

No host startup ordering, default target discovery, `IHostedService`, health/readiness endpoint, options binder, Elsa refusal policy, lifecycle subscriber, Nuplane integration, or package/module activation policy is added.
