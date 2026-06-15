# Configuring Shells

A shell is configured with a name, a list of enabled features, and an optional configuration section. CShells supports multiple ways to provide this configuration.

---

## Shell Settings Structure

Each shell has:

| Property | Description |
|---|---|
| `Name` | Unique shell identifier (e.g., `"Default"`, `"Acme"`) |
| `Features` | List of enabled feature names (or objects with inline settings) |
| `Configuration` | Shell-specific key/value configuration (hierarchical) |

---

## Option A: `appsettings.json`

The default configuration source. CShells reads from the `"CShells"` section by default.

```json
{
  "CShells": {
    "Shells": {
      "Default": {
        "Features": {
          "Core": {},
          "Weather": {}
        },
        "Configuration": {
          "WebRouting": {
            "Path": ""
          }
        }
      },
      "Admin": {
        "Features": {
          "Core": {},
          "Admin": { "MaxUsers": 100, "EnableAuditLog": true }
        },
        "Configuration": {
          "WebRouting": {
            "Path": "admin",
            "RoutePrefix": "api/v1"
          }
        }
      }
    }
  }
}
```

Register with the default section name:

```csharp
builder.AddShells();  // reads "CShells" section
```

Or specify a custom section:

```csharp
builder.AddShells("MyCustomSection");
```

---

## Option B: Code-First Configuration

Define shells directly in `Program.cs` using the fluent `AddShell` API:

```csharp
builder.AddShells(cshells =>
{
    cshells.AddShell("Default", shell => shell
        .WithFeatures("Core", "Weather")
        .WithConfiguration("WebRouting:Path", ""));

    cshells.AddShell("Admin", shell => shell
        .WithFeature("Core")
        .WithFeature("Admin", settings => settings
            .WithSetting("MaxUsers", 100)
            .WithSetting("EnableAuditLog", true))
        .WithConfiguration("WebRouting:Path", "admin")
        .WithConfiguration("WebRouting:RoutePrefix", "api/v1"));
});
```

You can also use type-safe feature references:

```csharp
cshells.AddShell("Default", shell => shell
    .WithFeature<CoreFeature>()
    .WithFeature<WeatherFeature>(f =>
    {
        // Set properties directly on the feature instance before ConfigureServices runs
        f.ApiKey = "my-key";
        f.TimeoutSeconds = 30;
    }));
```

The `Action<TFeature>` configurator runs after configuration binding but before `ConfigureServices`, so code always wins over `appsettings.json` values.

---

## Option C: FluentStorage (External JSON Files)

Load shells from individual JSON files. Useful for separating per-tenant configuration from the main config file and for cloud storage scenarios.

**Install:**

```bash
dotnet add package CShells.Providers.FluentStorage
dotnet add package FluentStorage
```

**Create shell JSON files** (e.g., `Shells/Default.json`):

```json
{
  "Name": "Default",
  "Features": ["Core", "Weather"],
  "Configuration": {
    "WebRouting": {
      "Path": ""
    }
  }
}
```

**Register the provider:**

```csharp
using FluentStorage;
using CShells.Providers.FluentStorage;

var shellsPath = Path.Combine(builder.Environment.ContentRootPath, "Shells");
var blobStorage = StorageFactory.Blobs.DirectoryFiles(shellsPath);

builder.AddShells(cshells =>
{
    cshells.WithFluentStorageProvider(blobStorage);
});
```

The FluentStorage provider supports Azure Blob Storage, AWS S3, and other backends in addition to local disk.

---

## Option D: Custom Blueprint Provider

Implement `IShellBlueprintProvider` to load shell blueprints from any source (database, API, etc.):

```csharp
using CShells.Lifecycle;

public class DatabaseShellBlueprintProvider : IShellBlueprintProvider
{
    private readonly AppDbContext _dbContext;

    public DatabaseShellBlueprintProvider(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProvidedBlueprint?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .SingleOrDefaultAsync(t => t.Id == name && t.IsActive, cancellationToken);

        if (tenant is null)
            return null;

        return new ProvidedBlueprint(new DatabaseShellBlueprint(tenant));
    }

    public Task<BlueprintPage> ListAsync(BlueprintListQuery query, CancellationToken cancellationToken = default) =>
        // Return paged BlueprintSummary rows for your source.
        throw new NotImplementedException();
}
```

Register it:

```csharp
builder.AddShells(cshells =>
{
    cshells.AddBlueprintProvider(sp => sp.GetRequiredService<DatabaseShellBlueprintProvider>());
});
```

`DatabaseShellBlueprint` is your `IShellBlueprint` implementation that composes fresh `ShellSettings` for a tenant.

---

## Provider Selection

CShells uses exactly one blueprint provider per host. Code-first `AddShell(...)` registrations use the built-in in-memory provider; external sources register a single provider through `AddBlueprintProvider(...)` or a provider-specific extension.

```csharp
builder.AddShells(cshells =>
{
    // Code-first provider:
    cshells.AddShell("Default", shell => shell.WithFeatures("Core"));
});

builder.AddShells(cshells =>
{
    // External configuration provider:
    cshells.WithConfigurationProvider(builder.Configuration);
});

builder.AddShells(cshells =>
{
    // Custom external provider:
    cshells.AddBlueprintProvider(sp => sp.GetRequiredService<DatabaseShellBlueprintProvider>());
});
```

Do not mix `AddShell(...)` and `AddBlueprintProvider(...)` on the same host. If you need several backing stores, implement one custom `IShellBlueprintProvider` that combines them internally.

See [Shell Blueprint Providers](Multiple-Shell-Providers) for provider patterns and examples.

---

## WebRouting Configuration

The `WebRouting` configuration section controls how a shell is matched to incoming requests and how its endpoints are prefixed.

| Key | Description | Example |
|---|---|---|
| `WebRouting:Path` | URL path prefix for shell resolution (empty string = root) | `"tenants/acme"` |
| `WebRouting:Host` | Hostname for host-based resolution | `"acme.example.com"` |
| `WebRouting:RoutePrefix` | Additional prefix applied to all shell endpoints | `"api/v1"` |

Example: with `Path = "acme"` and `RoutePrefix = "api/v1"`, an endpoint mapped at `"products"` is accessible at `/acme/api/v1/products`.

---

## Built-in Providers Reference

| Provider | Class | Use Case |
|---|---|---|
| Configuration | `ConfigurationShellBlueprintProvider` | `appsettings.json` and any `IConfiguration` source |
| In-memory | `InMemoryShellBlueprintProvider` | Code-first, testing |
| FluentStorage | `FluentStorageShellBlueprintProvider` | Files on disk or cloud storage |
