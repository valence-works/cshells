# ISettledShellRegistry

Namespace: `CShells.Lifecycle`, assembly/package: `CShells.Abstractions`.

```csharp
public interface ISettledShellRegistry
{
    IShell? GetSettledActive(string name);
}
```

The built-in `IShellRegistry` object implements this optional capability. Resolve the existing registry and cast it; no separate service registration is promised. Existing custom registries need not implement it. A failed cast means unsupported capability, whereas null from a supported capability means no current settled active generation.

The method validates the name consistently with `GetActive`; the built-in implementation matches names case insensitively. It reads only memory, never activates, resolves services, consults a blueprint provider, invokes user callbacks, or waits for activation/reload serialization.

Return the exact current Active generation only after successful activation settlement, including processing Complete callbacks and final eligibility. Complete callback errors preserve existing diagnostic-only semantics. Return null for absent, provisional, non-Active, or removed current state. During replacement publication, do not substitute an older retained generation. Before publication and after successful rollback, an eligible old current generation may be returned.

This is a point-in-time observation. Identity and active eligibility are rechecked before returning; concurrent mutation may change them immediately afterward. No scope, lease, disposal ownership, or future availability guarantee is granted. `GetActive` routing visibility and activation runner terminal success remain unchanged. Application health/failure reporting and unsupported-capability policy belong to the host.
