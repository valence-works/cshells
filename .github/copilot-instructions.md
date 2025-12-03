# Copilot Instructions for CShells

## Project Overview

CShells is a lightweight, extensible shell and feature system for .NET projects that enables building modular and multi-tenant applications with per-shell DI containers and config-driven features.

## Tech Stack

- **Language**: C# (.NET 10)
- **Framework**: .NET 10 with ASP.NET Core integration
- **Testing**: xUnit
- **Build**: MSBuild with Central Package Management (Directory.Packages.props)
- **CI/CD**: GitHub Actions

## Project Structure

- `src/CShells/` - Core library with shell and feature abstractions
- `src/CShells.AspNetCore/` - ASP.NET Core integration (middleware, endpoints)
- `src/CShells.Providers.FluentStorage/` - FluentStorage provider for shell configuration
- `tests/CShells.Tests/` - Unit and integration tests
- `tests/CShells.Tests.EndToEnd/` - End-to-end tests
- `samples/CShells.Workbench/` - Sample application demonstrating multi-tenant features
- `docs/` - Documentation

## Code Conventions

### C# Style

- Use C# 14 features where appropriate (primary constructors, collection expressions, etc.)
- Enable nullable reference types (`<Nullable>enable</Nullable>`)
- Use implicit usings (`<ImplicitUsings>enable</ImplicitUsings>`)
- Prefer file-scoped namespaces
- Always `var` when possible.
- Use expression-bodied members for single-line methods and properties

### Naming Conventions

- Use PascalCase for public members, types, and namespaces
- Use camelCase for private fields (without underscore prefix)
- Use `I` prefix for interfaces (e.g., `IShellFeature`)
- Use descriptive names that reflect purpose (e.g., `ShellFeatureAttribute`, `FeatureDependencyResolver`)

### Architecture Patterns

- Features implement `IShellFeature` for service registration or `IWebShellFeature` for services + endpoints
- Use the `[ShellFeature]` attribute to mark and configure features
- Features can declare dependencies on other features via the `DependsOn` property
- Each shell has its own isolated DI container
- Configuration is driven by `ShellSettings` and can come from appsettings.json, JSON files, or code

## Testing Guidelines

- Write unit tests using xUnit
- Use xUnit's Assert methods for assertions
- Place unit tests in `tests/CShells.Tests/Unit/`
- Place integration tests in `tests/CShells.Tests/Integration/`
- Test file names should match the class being tested with `Tests` suffix (e.g., `FeatureDiscoveryTests.cs`)

## Building and Testing

```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/CShells.Tests/

# Run the sample application
cd samples/CShells.Workbench && dotnet run
```

## Key Concepts

### Shells

Shells are isolated execution contexts with their own DI containers and feature sets. They can represent tenants, environments, or deployment configurations.

### Features

Features are modular units of functionality that can be enabled/disabled per shell. They implement `IShellFeature` or `IWebShellFeature` and are discovered via reflection using the `[ShellFeature]` attribute.

### Shell Resolvers

Shell resolvers determine which shell should handle a request. Built-in resolvers include path-based and host-based resolution.

## Common Tasks

### Adding a New Feature

1. Create a class implementing `IShellFeature` or `IWebShellFeature`
2. Add the `[ShellFeature("FeatureName")]` attribute
3. Implement `ConfigureServices` for DI registration
4. Implement `MapEndpoints` (if using `IWebShellFeature`) for routing

### Adding a New Shell Provider

1. Implement `IShellSettingsProvider`
2. Register it using the fluent API (e.g., `cshells.WithProvider<MyProvider>()`)

## Pull Request Guidelines

- Keep changes focused and minimal
- Include tests for new functionality
- Update documentation if adding new features
- Ensure all tests pass before submitting
