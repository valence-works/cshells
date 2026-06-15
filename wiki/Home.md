# CShells Wiki

Welcome to the CShells wiki! CShells is a lightweight, extensible shell and feature system for .NET projects that lets you build modular and multi-tenant applications with per-shell DI containers and config-driven features.

---

## What Is CShells?

CShells gives your application a structured way to define isolated **shells**, each with its own DI container, configuration, and set of enabled **features**. Features are modular units of functionality that register services and (optionally) HTTP endpoints.

- Each shell has its **own `IServiceProvider`** — services are fully isolated between shells.
- Features are **discovered automatically** by scanning assemblies for types that implement `IShellFeature`.
- Shell configuration comes from **`appsettings.json`**, external JSON files, code, a database, or any custom `IShellBlueprintProvider`.

---

## Use Cases

| Scenario | How CShells Helps |
|---|---|
| **Multi-tenant SaaS** | One shell per tenant, each with its own features, DI, and config |
| **Modular monolith** | Core, Billing, Reporting etc. as features that can be toggled per environment |
| **Environment/plan tiers** | Basic, Pro, Enterprise shells that enable different feature sets |
| **White-label deployments** | Per-brand shells with shared core and varying integrations |
| **Plugin-style extensions** | Features discovered from additional assemblies at startup |
| **API gateway / BFF** | Per-surface shells (mobile, web, partner) with tailored endpoints |
| **Platform / CMS frameworks** | Feature-based modules with per-shell DI, similar to Orchard Core or ABP |

---

## Packages

| Package | Purpose |
|---|---|
| **CShells.Abstractions** | Core interfaces (`IShellFeature`, `ShellSettings`, etc.) — reference in feature class libraries |
| **CShells.AspNetCore.Abstractions** | `IWebShellFeature`, `IMiddlewareShellFeature` — reference in ASP.NET Core feature libraries |
| **CShells** | Core runtime — reference in your main application |
| **CShells.AspNetCore** | ASP.NET Core integration (middleware, routing, resolvers) |
| **CShells.Providers.FluentStorage** | Load shell configs from disk or cloud storage |
| **CShells.FastEndpoints** | FastEndpoints integration for per-shell endpoint isolation |

---

## Navigation

| Page | What You'll Learn |
|---|---|
| [Getting Started](Getting-Started) | Installation, first feature, and running the app |
| [Creating Features](Creating-Features) | `IShellFeature`, `IWebShellFeature`, `IMiddlewareShellFeature`, `ShellFeatureAttribute` |
| [Feature Configuration](Feature-Configuration) | Inline settings, `IConfigurableFeature<T>`, validation |
| [Configuring Shells](Configuring-Shells) | `appsettings.json`, code-first, FluentStorage, custom providers, multiple providers |
| [Shell Resolution](Shell-Resolution) | Path, host, header, claim-based, and custom resolvers |
| [Runtime Shell Management](Runtime-Shell-Management) | Add, remove, and update shells at runtime |
| [Background Workers](Background-Workers) | Using `IShellRegistry` and `IShell.BeginScope()` in background services |
| [Integration Patterns](Integration-Patterns) | Safely integrating CShells into existing ASP.NET Core apps |
| [FastEndpoints Integration](FastEndpoints-Integration) | Per-shell FastEndpoints support |
| [Architecture](Architecture) | Internal design: shell host, feature discovery, DI, middleware |
