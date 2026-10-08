# Quickstart: Nuplane Observer Failure Diagnostics

The diagnostic behavior is automatic when the optional Nuplane adapter is selected and the host has a logging provider. No new adapter option or public logging API is required.

```csharp
using CShells.DependencyInjection;
using CShells.Nuplane;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();
services.AddCShells(shells => shells.WithNuplaneFeatureDiscovery());
```

If the host does not register logging, the adapter uses its null logger fallback and retains the existing exception behavior.

## Focused validation

Run from the repository root through the standard `dotnet` build-slot wrapper:

```sh
dotnet test tests/CShells.Tests/CShells.Tests.csproj -c Release --filter 'FullyQualifiedName~Nuplane'
dotnet build src/CShells.Nuplane/CShells.Nuplane.csproj -c Release -f net8.0
dotnet build src/CShells.Nuplane/CShells.Nuplane.csproj -c Release -f net9.0
dotnet build src/CShells.Nuplane/CShells.Nuplane.csproj -c Release -f net10.0
dotnet test tests/CShells.Tests/CShells.Tests.csproj -c Release
```

Before issue closure, pass the required organization PR/review and main-branch CI gates, then use the separate PackageReference-only diagnostic consumer against the resulting owner-published corrective preview on actual .NET 8, .NET 9, and .NET 10 runtimes. Audit all ten CShells archives against successful owner workflow metadata and public-feed bytes first. The pre-correction preview counterexample is not correction proof. No manual package publication, stable-pin change, or stable-release qualification belongs to this work unit.
