# Feature Configuration

CShells provides an elegant, convention-based configuration system that allows features to receive settings from `appsettings.json`, environment variables, or any other `IConfiguration` source.

## Overview

Features can be configured in three ways:

1. **Inline Configuration** - Settings are defined directly with the feature in the Features array
2. **Explicit Configuration** - Implement `IConfigurableFeature<TOptions>` for strongly-typed options
3. **Manual Configuration** - Use `IConfiguration` or `IOptions<T>` directly in `ConfigureServices`

## Inline Configuration (Recommended)

The most elegant way to configure a feature is inline, where the feature name and settings are colocated in the Features array. Each feature can be either:
- A simple string (feature name only)
- An object with `Name` and any settings as properties

### Configuration (appsettings.json)

```json
{
  "CShells": {
    "Shells": [
      {
        "Name": "Default",
        "Features": [
          "Core",
          {
            "Name": "SqliteWorkflowPersistence",
            "ConnectionString": "Data Source=production.db;Cache=Shared",
            "EnableSensitiveDataLogging": false,
            "CommandTimeout": 60
          },
          "Logging"
        ]
      }
    ]
  }
}
```

### Feature Implementation

```csharp
[ShellFeature(
    DisplayName = "SQLite Workflow Persistence",
    Description = "Provides SQLite persistence for workflows")]
public class SqliteWorkflowPersistenceFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Bind settings from the feature's configuration section
        services.AddOptions<SqliteWorkflowPersistenceOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection("SqliteWorkflowPersistence").Bind(options);
            });

        services.AddDbContext<WorkflowDbContext>((sp, options) =>
        {
            var persistenceOptions = sp.GetRequiredService<IOptions<SqliteWorkflowPersistenceOptions>>().Value;
            options.UseSqlite(persistenceOptions.ConnectionString);
        });
    }
}

public class SqliteWorkflowPersistenceOptions
{
    public string ConnectionString { get; set; } = "Data Source=workflows.db";
    public bool EnableSensitiveDataLogging { get; set; }
    public int CommandTimeout { get; set; } = 30;
}
```

### Environment Variables

Environment variables override configuration file settings using hierarchical naming:

```bash
# Override connection string for specific shell and feature
Shells__Default__Features__1__ConnectionString="Data Source=prod.db"

# Or use environment-specific secrets
ConnectionStrings__Workflows="Server=prod;Database=Workflows;..."
```

## Explicit Configuration with IConfigurableFeature<T>

For more complex configuration scenarios, implement `IConfigurableFeature<TOptions>`. This provides:

- Strongly-typed options classes
- Separation of concerns (feature logic vs. configuration)
- Support for multiple options classes per feature
- Easier testing

### Example

```csharp
// Options class
public class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
    public int CommandTimeout { get; set; } = 30;
    public bool EnableRetryOnFailure { get; set; } = true;
}

// Feature with explicit configuration
[ShellFeature(DisplayName = "Database Feature")]
public class DatabaseFeature : IShellFeature, IConfigurableFeature<DatabaseOptions>
{
    private DatabaseOptions _options = new();

    public void Configure(DatabaseOptions options)
    {
        // Called automatically after options are bound from configuration
        _options = options;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>(opts =>
        {
            opts.UseSqlServer(_options.ConnectionString);
            if (_options.EnableRetryOnFailure)
                opts.EnableRetryOnFailure();
        });
    }
}
```

### Configuration

```json
{
  "CShells": {
    "Shells": [
      {
        "Name": "Default",
        "Features": [
          "Core",
          { "Name": "Database", "ConnectionString": "Server=localhost;Database=App;...", "CommandTimeout": 60, "EnableRetryOnFailure": true }
        ]
      }
    ]
  }
}
```

Alternatively, use the shell's `Configuration` section for shared settings:

```json
{
  "CShells": {
    "Shells": [
      {
        "Name": "Default",
        "Features": ["Core", "Database"],
        "Configuration": {
          "Database": {
            "ConnectionString": "Server=localhost;Database=App;...",
            "CommandTimeout": 60,
            "EnableRetryOnFailure": true
          }
        }
      }
    ]
  }
}
```

## Multiple Options Classes

A feature can implement multiple `IConfigurableFeature<T>` interfaces to bind different configuration sections:

```csharp
public class MessagingOptions
{
    public string QueueName { get; set; } = "default";
    public int MaxRetries { get; set; } = 3;
}

public class CacheOptions
{
    public int ExpirationMinutes { get; set; } = 60;
    public string Provider { get; set; } = "Memory";
}

[ShellFeature(DisplayName = "Advanced Feature")]
public class AdvancedFeature : IShellFeature,
    IConfigurableFeature<MessagingOptions>,
    IConfigurableFeature<CacheOptions>
{
    private MessagingOptions _messagingOptions = new();
    private CacheOptions _cacheOptions = new();

    public void Configure(MessagingOptions options)
    {
        _messagingOptions = options;
    }

    public void Configure(CacheOptions options)
    {
        _cacheOptions = options;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Use both options
    }
}
```

## Configuration Validation

CShells supports three validation strategies:

### 1. DataAnnotations (Default)

Use standard DataAnnotations attributes:

```csharp
public class DatabaseOptions
{
    [Required(ErrorMessage = "ConnectionString is required")]
    [MinLength(10)]
    public string ConnectionString { get; set; } = "";

    [Range(10, 300)]
    public int CommandTimeout { get; set; } = 30;
}
```

DataAnnotations validation is enabled by default. No additional setup required.

### 2. FluentValidation

For complex validation logic, use FluentValidation:

```csharp
// Install: FluentValidation.DependencyInjectionExtensions

public class DatabaseOptionsValidator : AbstractValidator<DatabaseOptions>
{
    public DatabaseOptionsValidator()
    {
        RuleFor(x => x.ConnectionString)
            .NotEmpty()
            .Must(BeValidConnectionString)
            .WithMessage("Invalid connection string format");

        RuleFor(x => x.CommandTimeout)
            .InclusiveBetween(10, 300);
    }

    private bool BeValidConnectionString(string connectionString)
    {
        // Custom validation logic
        return !string.IsNullOrWhiteSpace(connectionString);
    }
}
```

Register FluentValidation in your application startup:

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentFeatureValidation();
```

### 3. Composite Validation

Combine multiple validators:

```csharp
builder.Services.AddCompositeFeatureValidation(validators =>
{
    validators.Add(new DataAnnotationsFeatureConfigurationValidator());
    validators.Add<FluentValidationFeatureConfigurationValidator>();
    validators.Add<CustomValidator>();
});
```

### 4. Custom Validation

Implement `IFeatureConfigurationValidator` for custom validation logic:

```csharp
public class CustomValidator : IFeatureConfigurationValidator
{
    public void Validate(object target, string contextName)
    {
        if (target is DatabaseOptions options)
        {
            if (options.ConnectionString.Contains("password="))
            {
                throw new FeatureConfigurationValidationException(
                    contextName,
                    new[] { "Connection string should not contain plain-text passwords" });
            }
        }
    }
}

// Register
builder.Services.AddCustomFeatureValidation<CustomValidator>();
```

## Configuration Hierarchies

Configuration follows a precedence order (highest to lowest):

1. **Environment Variables** - `Shells__Default__Configuration__FeatureName__PropertyName`
2. **Inline Feature Configuration** - Settings defined with the feature in the Features array
3. **Shell Configuration** - `CShells:Shells[].Configuration.FeatureName`
4. **Root Configuration** - `FeatureName:PropertyName`
5. **Feature Defaults** - Property default values

### Example

```json
{
  "Database": {
    "ConnectionString": "DefaultConnection"
  },
  "CShells": {
    "Shells": [
      {
        "Name": "Default",
        "Features": ["Core", "Database"],
        "Configuration": {
          "Database": {
            "ConnectionString": "ShellSpecificConnection"
          }
        }
      }
    ]
  }
}
```

With environment variable:
```bash
Shells__Default__Configuration__Database__ConnectionString="EnvironmentConnection"
```

The feature will use `"EnvironmentConnection"` (environment variables win).

## Secrets Management

Never store secrets in `appsettings.json`. Use environment variables or secret managers:

### Development (User Secrets)

```bash
dotnet user-secrets set "Shells:Default:Configuration:Database:ConnectionString" "Server=localhost;..."
```

### Production (Environment Variables)

```bash
# Docker
docker run -e Shells__Default__Configuration__Database__ConnectionString="Server=prod;..." myapp

# Kubernetes
apiVersion: v1
kind: Secret
metadata:
  name: app-secrets
stringData:
  connection-string: "Server=prod;..."
```

### Azure Key Vault / AWS Secrets Manager

CShells works seamlessly with any `IConfiguration` provider:

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://myvault.vault.azure.net/"),
    new DefaultAzureCredential());
```

Configuration keys in Key Vault:
- `Shells--Default--Configuration--Database--ConnectionString`

## Best Practices

1. **Use Auto-Configuration for Simple Properties**
   - Less boilerplate
   - Faster to implement
   - Good for most scenarios

2. **Use IConfigurableFeature<T> for Complex Configuration**
   - Multiple related settings
   - Shared options across features
   - Need for custom validation

3. **Validate Configuration**
   - Use `[Required]` for mandatory settings
   - Provide meaningful error messages
   - Fail fast at startup

4. **Provide Sensible Defaults**
   - Features should work with minimal configuration
   - Document required vs. optional settings

5. **Never Commit Secrets**
   - Use environment variables in production
   - Use User Secrets in development
   - Use secret managers for sensitive data

6. **Document Configuration**
   - Provide example appsettings.json
   - Document all properties and their defaults
   - Include validation requirements

## Migration from Manual Configuration

### Before (Manual)

```csharp
public class OldFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        var config = services.BuildServiceProvider()
            .GetRequiredService<IConfiguration>();

        var connectionString = config["Database:ConnectionString"];
        // ... manual configuration logic
    }
}
```

### After (Auto-Configuration)

```csharp
public class NewFeature : IShellFeature
{
    public string ConnectionString { get; set; } = "default";

    public void ConfigureServices(IServiceCollection services)
    {
        // ConnectionString is already configured!
    }
}
```

## Troubleshooting

### Configuration Not Applied

1. **Check feature name matches configuration section**
   - Feature name from `[ShellFeature]` attribute or class name
   - Configuration section in `Configuration.{FeatureName}` or inline in Features array

2. **Check property is public and settable**
   ```csharp
   // ✅ Works
   public string ConnectionString { get; set; }

   // ❌ Doesn't work (private setter)
   public string ConnectionString { get; private set; }
   ```

3. **Enable debug logging**
   ```json
   {
     "Logging": {
       "LogLevel": {
         "CShells": "Debug"
       }
     }
   }
   ```

### Validation Errors

Check logs for detailed validation messages:

```
Configuration validation failed for 'DatabaseFeature': ConnectionString is required
```

Fix by providing the required configuration.

### Type Conversion Errors

Ensure configuration values match property types:

```json
{
  "FeatureName": {
    "Port": 5000,           // ✅ int
    "Port": "5000",         // ❌ string (won't bind to int)
    "Enabled": true,        // ✅ bool
    "Enabled": "true"       // ✅ Also works (converted automatically)
  }
}
```

## Summary

CShells' feature configuration system provides:

- ✅ **Zero boilerplate** for simple scenarios
- ✅ **Strongly-typed options** for complex scenarios
- ✅ **Automatic validation** with DataAnnotations or FluentValidation
- ✅ **Environment variable support** for secrets
- ✅ **IConfiguration integration** with any configuration provider
- ✅ **Shell-specific configuration** with precedence rules
- ✅ **Fail-fast validation** at startup

This eliminates manual configuration plumbing while maintaining flexibility and type safety.
