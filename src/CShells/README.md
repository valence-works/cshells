# CShells Configuration

CShells supports configuring shells and their enabled features via the application's configuration (appsettings.json). The default configuration section name is `CShells`.

Example appsettings.json (minimal):

```json
{
  "CShells": {
    "Shells": [
      {
        "Name": "Default",
        "Features": [ "Core", "Weather" ],
        "Properties": {
          "Title": "Default site"
        }
      },
      {
        "Name": "Admin",
        "Features": [ "Core", "Admin" ],
        "Properties": {
          "Title": "Admin area"
        }
      }
    ]
  }
}
```

Binding and registration
- Use Microsoft.Extensions.Configuration to bind the `CShells` section to `CShells.Configuration.CShellsOptions`.
- Convert the bound DTOs into runtime `ShellSettings` using `CShells.Configuration.ShellSettingsFactory.CreateFromOptions`.
- For convenience, call `services.AddCShells(configuration)` to bind the configuration and register `IShellHost` (DefaultShellHost) and the configured shells.

Notes
- Shell names are case-insensitive and must be unique. The factory will throw an exception for duplicate names.
- Feature names are trimmed and preserved in the configured order.
