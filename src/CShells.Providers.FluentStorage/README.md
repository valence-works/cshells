# CShells.Providers.FluentStorage

FluentStorage integration provider for CShells enabling shell configuration storage in various backends.

## Purpose

This package provides a shell settings provider that uses FluentStorage to load and persist shell configurations from various storage backends including local disk, Azure Blob Storage, AWS S3, and more.

## When to Use

- Store shell configurations as individual JSON files instead of appsettings.json
- Load shell configurations from cloud storage (Azure Blob, AWS S3)
- Enable dynamic shell configuration updates without application restart
- Separate shell configuration from application configuration
- Multi-environment deployments with centralized configuration storage

## Supported Storage Backends

FluentStorage supports numerous storage providers:

- **Local disk** - Directory-based file storage
- **Azure Blob Storage** - Azure cloud storage
- **AWS S3** - Amazon cloud storage
- **In-memory** - Temporary storage for testing
- And many more FluentStorage providers

## Installation

```bash
dotnet add package CShells.Providers.FluentStorage
dotnet add package FluentStorage
# Add specific FluentStorage provider packages as needed
# e.g., FluentStorage.Azure.Blobs, FluentStorage.AWS
```

## Quick Start

### Local Disk Storage

Create JSON files in a `Shells` folder (e.g., `Default.json`, `Admin.json`):

**Shells/Default.json:**
```json
{
  "Name": "Default",
  "Features": [ "Core", "Weather" ],
  "Properties": {
    "WebRouting": {
      "Path": ""
    }
  }
}
```

**Shells/Admin.json:**
```json
{
  "Name": "Admin",
  "Features": [ "Core", "Admin" ],
  "Properties": {
    "WebRouting": {
      "Path": "admin"
    }
  }
}
```

**Program.cs:**
```csharp
using FluentStorage;
using CShells.Providers.FluentStorage;

var builder = WebApplication.CreateBuilder(args);

var shellsPath = Path.Combine(builder.Environment.ContentRootPath, "Shells");
var blobStorage = StorageFactory.Blobs.DirectoryFiles(shellsPath);

builder.AddShells(cshells =>
{
    cshells.WithFluentStorageProvider(blobStorage);
});

var app = builder.Build();
app.MapShells();
app.Run();
```

### Azure Blob Storage

```csharp
using FluentStorage;
using FluentStorage.Azure.Blobs;
using CShells.Providers.FluentStorage;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AzureStorage");
var blobStorage = StorageFactory.Blobs.AzureBlobStorage(connectionString, "shells");

builder.AddShells(cshells =>
{
    cshells.WithFluentStorageProvider(blobStorage);
});

var app = builder.Build();
app.MapShells();
app.Run();
```

### AWS S3 Storage

```csharp
using FluentStorage;
using FluentStorage.AWS;
using CShells.Providers.FluentStorage;

var builder = WebApplication.CreateBuilder(args);

var blobStorage = StorageFactory.Blobs.AwsS3(
    accessKeyId: "your-access-key",
    secretAccessKey: "your-secret-key",
    bucketName: "shells",
    region: "us-east-1"
);

builder.AddShells(cshells =>
{
    cshells.WithFluentStorageProvider(blobStorage);
});

var app = builder.Build();
app.MapShells();
app.Run();
```

## File Naming Convention

Shell configuration files should be named with the shell name followed by `.json`:

- `Default.json` → Shell with `Name: "Default"`
- `Acme.json` → Shell with `Name: "Acme"`
- `Contoso.json` → Shell with `Name: "Contoso"`

## Configuration Format

Each JSON file should contain a complete shell configuration:

```json
{
  "Name": "ShellName",
  "Features": [ "Feature1", "Feature2" ],
  "Properties": {
    "WebRouting": {
      "Path": "path",
      "Host": "example.com"
    },
    "CustomProperty": "value"
  }
}
```

## Benefits

- **Separation of concerns** - Shell configurations separate from application settings
- **Dynamic updates** - Update shell configurations without rebuilding or redeploying
- **Centralized management** - Store configurations in cloud storage accessible across deployments
- **Version control friendly** - Individual files are easier to track and manage
- **Environment-specific** - Different storage backends for different environments

## Learn More

- [Main Documentation](https://github.com/sfmskywalker/cshells)
- [FluentStorage Documentation](https://github.com/robinrodricks/FluentStorage)
- [CShells Package](../CShells) - Core runtime
