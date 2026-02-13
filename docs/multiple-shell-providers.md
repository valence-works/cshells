# Multiple Shell Providers

CShells now supports registering multiple shell providers that work together seamlessly. This allows you to load shells from various sources simultaneously.

## Overview

The multi-provider architecture enables you to:

- ✅ **Combine multiple sources**: Load shells from configuration, databases, APIs, and code
- ✅ **Code-first + Providers**: Mix code-first shells with provider-based shells
- ✅ **Override semantics**: Later providers can override shells from earlier providers (by shell ID)
- ✅ **Extensible**: Easily add custom providers without replacing existing ones
- ✅ **Zero configuration**: If no providers are registered, an empty provider is used automatically

## Basic Usage

### Code-First Shells Only

```csharp
builder.AddShells(shells =>
{
    shells.AddShell("Tenant1", shell =>
    {
        shell.WithFeatures("Core", "Premium");
    });
    
    shells.AddShell("Tenant2", shell =>
    {
        shell.WithFeatures("Core");
    });
});
```

### Configuration Provider Only

```csharp
builder.AddShells(shells =>
{
    shells.WithConfigurationProvider(builder.Configuration);
});
```

### Multiple Providers

```csharp
builder.AddShells(shells =>
{
    // 1. Code-first shells (loaded first)
    shells.AddShell("Default", shell => 
        shell.WithFeatures("Core"));
    
    // 2. Configuration-based shells (loaded second)
    shells.WithConfigurationProvider(builder.Configuration);
    
    // 3. Database-based shells (loaded third)
    shells.WithProvider<DatabaseShellSettingsProvider>();
    
    // 4. Custom provider via factory (loaded fourth)
    shells.WithProvider(sp => 
        new CustomShellProvider(sp.GetRequiredService<IMyService>()));
});
```

## Provider Order and Overrides

Providers are queried in the order they're registered:

1. Code-first shells (via `AddShell`)
2. First `WithProvider` / `WithConfigurationProvider` call
3. Second provider
4. Third provider
5. ... and so on

If multiple providers return a shell with the same ID, **the last provider wins**.

### Example: Override Pattern

```csharp
builder.AddShells(shells =>
{
    // Base configuration from appsettings.json
    shells.WithConfigurationProvider(builder.Configuration);
    
    // Override specific shells from database (for runtime changes)
    shells.WithProvider<DatabaseShellSettingsProvider>();
    
    // Override for development/testing
    if (builder.Environment.IsDevelopment())
    {
        shells.AddShell("Tenant1", shell => 
            shell.WithFeatures("Core", "Debug"));
    }
});
```

In this example:
- Most shells come from `appsettings.json`
- Database can override any shell for runtime updates
- Development environment can override "Tenant1" with debug features

## Built-in Providers

### InMemoryShellSettingsProvider

Immutable in-memory provider (used internally for code-first shells):

```csharp
var shells = new List<ShellSettings> { /* ... */ };
var provider = new InMemoryShellSettingsProvider(shells);
```

### MutableInMemoryShellSettingsProvider

Thread-safe, mutable in-memory provider for dynamic scenarios:

```csharp
var provider = new MutableInMemoryShellSettingsProvider();

// Add or update shells at runtime
provider.AddOrUpdate(shellSettings);

// Remove shells
provider.Remove(new ShellId("Tenant1"));

// Clear all
provider.Clear();

// Use as provider
shells.WithProvider(provider);
```

### ConfigurationShellSettingsProvider

Loads shells from `IConfiguration` (typically `appsettings.json`):

```csharp
shells.WithConfigurationProvider(builder.Configuration, "CShells");
```

### CompositeShellSettingsProvider

Aggregates multiple providers (used internally when multiple providers are registered):

```csharp
var providers = new List<IShellSettingsProvider>
{
    new InMemoryShellSettingsProvider(codeFirstShells),
    new ConfigurationShellSettingsProvider(configuration),
    new DatabaseShellSettingsProvider(dbContext)
};

var composite = new CompositeShellSettingsProvider(providers);
```

## Custom Providers

Implement `IShellSettingsProvider` to create custom providers:

```csharp
public class DatabaseShellSettingsProvider : IShellSettingsProvider
{
    private readonly MyDbContext _dbContext;
    
    public DatabaseShellSettingsProvider(MyDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<IEnumerable<ShellSettings>> GetShellSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var tenants = await _dbContext.Tenants
            .Where(t => t.IsActive)
            .ToListAsync(cancellationToken);
        
        return tenants.Select(tenant => new ShellSettings(
            new ShellId(tenant.Id.ToString()),
            tenant.EnabledFeatures.ToList()));
    }
}
```

Register it:

```csharp
// Register as type (resolved from DI)
shells.WithProvider<DatabaseShellSettingsProvider>();

// Register as instance
shells.WithProvider(new DatabaseShellSettingsProvider(dbContext));

// Register via factory
shells.WithProvider(sp => 
    new DatabaseShellSettingsProvider(sp.GetRequiredService<MyDbContext>()));
```

## Real-World Scenarios

### Multi-Tenant SaaS

```csharp
builder.AddShells(shells =>
{
    // Default shell for new tenants
    shells.AddShell("Default", shell => 
        shell.WithFeatures("Core", "Starter"));
    
    // Active tenants from database
    shells.WithProvider<ActiveTenantsProvider>();
    
    // Feature overrides from configuration
    shells.WithConfigurationProvider(builder.Configuration, "TenantOverrides");
});
```

### Development + Production

```csharp
builder.AddShells(shells =>
{
    if (builder.Environment.IsProduction())
    {
        // Production: Load from Azure Blob Storage
        shells.WithProvider<AzureBlobShellProvider>();
    }
    else
    {
        // Development: Use local configuration
        shells.WithConfigurationProvider(builder.Configuration);
        
        // Add test tenants
        shells.AddShell("TestTenant1", shell => shell.WithFeatures("All"));
        shells.AddShell("TestTenant2", shell => shell.WithFeatures("Core"));
    }
});
```

### Progressive Migration

```csharp
builder.AddShells(shells =>
{
    // Legacy shells from old system (via API)
    shells.WithProvider<LegacySystemProvider>();
    
    // Migrated shells from new database
    shells.WithProvider<NewDatabaseProvider>();
    
    // Migrated shells override legacy ones (by ID)
    // This allows gradual migration
});
```

## Runtime Shell Management

The `IShellManager` supports adding/removing shells at runtime, which works with any provider:

```csharp
public class TenantService
{
    private readonly IShellManager _shellManager;
    
    public TenantService(IShellManager shellManager)
    {
        _shellManager = shellManager;
    }
    
    public async Task CreateTenantAsync(string tenantId, List<string> features)
    {
        var settings = new ShellSettings(new ShellId(tenantId), features);
        await _shellManager.AddShellAsync(settings);
    }
    
    public async Task DeleteTenantAsync(string tenantId)
    {
        await _shellManager.RemoveShellAsync(new ShellId(tenantId));
    }
}
```

## Best Practices

1. **Order Matters**: Register providers from most general to most specific
2. **Override Intentionally**: Use later providers to override earlier ones when needed
3. **Development Overrides**: Add development-specific shells last to override production config
4. **Empty Providers**: If no providers are registered, CShells works fine with zero shells
5. **Thread Safety**: Built-in providers are thread-safe; ensure custom providers are too
6. **Caching**: Providers are called once at startup (and when `ReloadAllShellsAsync` is called)

## Migration from Old API

### Before (Single Provider)

```csharp
// Old way - only one provider allowed
builder.AddShells(shells =>
{
    shells.WithConfigurationProvider(builder.Configuration);
    // Could NOT add another provider
});
```

### After (Multiple Providers)

```csharp
// New way - multiple providers work together
builder.AddShells(shells =>
{
    shells.WithConfigurationProvider(builder.Configuration);
    shells.WithProvider<DatabaseProvider>();
    shells.AddShell("Extra", shell => shell.WithFeatures("Core"));
});
```

The `WithInMemoryShells()` method is no longer needed - code-first shells are automatically registered.

