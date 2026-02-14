# CShells.AspNetCore.Testing

Testing utilities for CShells ASP.NET Core applications.

## Purpose

This package provides testing helpers and utilities for integration testing CShells-based ASP.NET Core applications.

## Key Features

- **`ShellInitializationWaiter`** - Helper to wait for shell initialization in integration tests

## Installation

```bash
dotnet add package CShells.AspNetCore.Testing
```

## Usage

### Waiting for Shell Initialization

In integration tests, use `ShellInitializationWaiter` to ensure all shells are initialized before running tests:

```csharp
using CShells.AspNetCore.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class MyIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MyIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TestShellEndpoint()
    {
        var client = _factory.CreateClient();
        
        // Wait for shells to be initialized
        await ShellInitializationWaiter.WaitForShellsAsync(_factory.Services);
        
        // Now run your tests
        var response = await client.GetAsync("/tenant1/api/weather");
        response.EnsureSuccessStatusCode();
    }
}
```

## Related Packages

- **CShells.AspNetCore** - ASP.NET Core integration (required)
- **Microsoft.AspNetCore.Mvc.Testing** - ASP.NET Core integration testing

