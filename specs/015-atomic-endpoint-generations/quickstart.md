# Verification

```bash
dotnet test tests/CShells.Tests/CShells.Tests.csproj --filter "FullyQualifiedName~DynamicShellEndpointDataSource|FullyQualifiedName~ShellEndpointRegistrationHandler|FullyQualifiedName~ShellMiddleware"
dotnet test tests/CShells.Tests/CShells.Tests.csproj
dotnet build CShells.sln
```
