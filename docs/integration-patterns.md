# CShells Integration Patterns with Host Applications

This guide explains how to safely integrate CShells into existing ASP.NET Core applications without conflicts.

## Overview

CShells uses **endpoint routing** to register shell-specific endpoints dynamically via the `IWebShellFeature` interface. When integrating CShells into an existing application, you need to ensure that shell routes don't conflict with your host application's routes.

**Note:** Only implement `IWebShellFeature` if your features need to configure HTTP endpoints. For features that only register services, implement `IShellFeature` instead.

## Safe Integration Patterns

### ✅ Pattern 1: Dedicated Path Prefixes (Recommended)

Use unique path prefixes for each shell to isolate shell routes from host routes:

```json
// Acme.json
{
  "Name": "Acme",
  "Features": ["Core", "Payment"],
  "Configuration": {
    "WebRouting": {
      "Path": "tenants/acme"
    }
  }
}

// Contoso.json
{
  "Name": "Contoso",
  "Features": ["Core", "Payment"],
  "Configuration": {
    "WebRouting": {
      "Path": "tenants/contoso"
    }
  }
}
```

**Result:**
- Shell routes: `/tenants/acme/*`, `/tenants/contoso/*`
- Host routes: Any other paths (e.g., `/api/*`, `/admin/*`, `/`)
- **No conflicts!**

### ✅ Pattern 2: Subdomain-Based Isolation

Use host-based routing to isolate shells by subdomain:

```json
// Acme.json
{
  "Name": "Acme",
  "Configuration": {
    "WebRouting": {
      "Host": "acme.example.com"
    }
  }
}
```

```csharp
// Program.cs
var blobStorage = StorageFactory.Blobs.DirectoryFiles("./Shells");

builder.AddShells(cshells =>
{
    cshells.WithFluentStorageProvider(blobStorage);
    // Standard resolvers (path and host-based) are registered by default
});
```

**Result:**
- `acme.example.com` → Acme shell routes
- `contoso.example.com` → Contoso shell routes
- `www.example.com` or `example.com` → Host application routes
- **No conflicts!**

### ✅ Pattern 3: Mixed Mode (Host + Shells)

Combine host routes with shell routes using careful path planning:

```csharp
var app = builder.Build();

// Register host routes FIRST (before MapShells)
app.MapGet("/", () => "Host Home Page");
app.MapControllers(); // Host API controllers at /api/*

// Register shell routes
app.MapShells(); // Shell routes at configured prefixes
```

**Best Practices:**
- ✅ Host routes without path prefixes (e.g., `/`, `/api/*`, `/admin/*`)
- ✅ Shell routes with path prefixes (e.g., `/apps/*`, `/tenants/*`)
- ✅ Register host routes BEFORE `MapShells()`

### ⚠️ Pattern 4: Root-Level Shell (Advanced)

Use an empty path prefix when the **entire application** is multi-tenant:

```json
{
  "Name": "Default",
  "Configuration": {
    "WebRouting": {
      "Path": ""
    }
  }
}
```

**⚠️ Warning:**
- Shell routes will match **ALL requests** at the root level
- Host application routes may be **shadowed** by shell routes
- Only use this pattern if your entire app is multi-tenant with no shared host routes

## Conflict Detection

CShells automatically detects and warns about path conflicts during endpoint registration:

```
warn: Path conflict detected: Shell 'Acme' endpoint 'GET /api/users' conflicts with
      shell 'Contoso' endpoint 'GET /api/users'. This may cause routing ambiguity.
```

```
warn: Path conflict detected: Shell 'Default' endpoint 'GET /' conflicts with host
      application endpoint 'GET /'. Shell routes may override host routes.
```

**When you see these warnings:**
1. Review your shell path configurations
2. Ensure shells use unique path prefixes
3. Consider subdomain-based routing if path-based routing causes conflicts

## Configuration Options

### Exclude Paths from Shell Resolution

Prevent shell resolution for specific paths (e.g., admin panels, health checks):

```csharp
builder.AddShells(cshells =>
{
    cshells.WithFluentStorageProvider(blobStorage);
    cshells.WithWebRoutingResolver(options =>
    {
        // Exclude these paths from shell resolution
        options.ExcludePaths = ["/admin", "/health", "/swagger"];
    });
});
```

**Result:**
- Requests to `/admin/*`, `/health`, `/swagger/*` → Never resolve to shells
- Other requests → Normal shell resolution applies

## Middleware Ordering

**Correct order is critical:**

```csharp
var app = builder.Build();

// 1. Exception handling
app.UseExceptionHandler("/Error");

// 2. HTTPS redirection
app.UseHttpsRedirection();

// 3. Static files (host application)
app.UseStaticFiles();

// 4. Routing
app.UseRouting();

// 5. Authentication/Authorization
app.UseAuthentication();
app.UseAuthorization();

// 6. Host routes (register before shell routes)
app.MapControllers();

// 7. Shell routes (includes middleware)
app.MapShells();
```

## Troubleshooting

### Issue: "Ambiguous match" errors

**Symptoms:**
```
Microsoft.AspNetCore.Routing.Matching.AmbiguousMatchException:
The request matched multiple endpoints
```

**Solutions:**
1. Check logs for path conflict warnings
2. Ensure each shell has a unique path prefix
3. Review shell JSON configurations
4. Use `options.ExcludePaths` to protect host routes

### Issue: Host routes not working

**Symptoms:**
- Requests to host routes return 404
- Shell routes work but host routes don't

**Solutions:**
1. Ensure host routes are registered before `MapShells()`
2. Check if a root-level shell (`Path: ""`) is shadowing host routes
3. Use path exclusions to protect host routes
4. Verify middleware ordering (host routes before `MapShells()`)

### Issue: Shell routes not working

**Symptoms:**
- All requests go to host application
- Shell endpoints return 404

**Solutions:**
1. Verify `MapShells()` is called in the pipeline
2. Check shell configuration files have correct path prefixes (`Configuration.WebRouting.Path`)
3. Ensure shell settings are loaded (check logs: "Loaded N shell(s)")
4. Verify shell resolver is configured (defaults to path and host routing)

## Example: Complete Integration

```csharp
// Program.cs
using FluentStorage;
using CShells.Providers.FluentStorage;

var builder = WebApplication.CreateBuilder(args);

// Host services
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

// CShells services
var shellsPath = Path.Combine(builder.Environment.ContentRootPath, "Shells");
var blobStorage = StorageFactory.Blobs.DirectoryFiles(shellsPath);

builder.AddShells(cshells =>
{
    cshells.WithFluentStorageProvider(blobStorage);
    cshells.WithWebRoutingResolver(options =>
    {
        // Protect host routes from shell resolution
        options.ExcludePaths = ["/api", "/swagger", "/health"];
    });
});

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Host routes first
app.MapGet("/", () => "Host Home");
app.MapControllers(); // /api/*
app.MapHealthChecks("/health");

// Shell routes (under /tenants/*)
app.MapShells();

app.Run();
```

### Example Feature Implementations

**Service-only feature (IShellFeature):**

```csharp
using CShells.Features;
using Microsoft.Extensions.DependencyInjection;

// No [ShellFeature] attribute needed - feature name will be "Payment"
public class PaymentFeature : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPaymentProcessor, StripePaymentProcessor>();
    }
}
```

**Web feature with endpoints (IWebShellFeature):**

```csharp
using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.Extensions.DependencyInjection;

// Using [ShellFeature] to specify explicit name and dependencies
[ShellFeature("Payment", DisplayName = "Payment API", DependsOn = ["Core"])]
public class PaymentFeature : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPaymentProcessor, StripePaymentProcessor>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        endpoints.MapPost("payment/process", (PaymentRequest request, IPaymentProcessor processor) =>
            processor.Process(request));
    }
}
```

## Best Practices Summary

✅ **DO:**
- Use dedicated path prefixes for shells (`/tenants/*`, `/apps/*`)
- Use subdomain-based routing for complete isolation
- Register host routes before `MapShells()`
- Use path exclusions to protect critical host routes
- Monitor logs for path conflict warnings

❌ **DON'T:**
- Use empty path prefixes unless entire app is multi-tenant
- Mix root-level shell routes with root-level host routes
- Ignore path conflict warnings in logs
- Register `MapShells()` before host routes

## Further Reading

- [Shell Resolution Strategies](./shell-resolution.md)
- [Dynamic Shell Management](./shell-management.md)
- [Endpoint Routing Architecture](./endpoint-routing.md)
