# Quickstart: Share Host Singletons with Shells

1. Register the service on the host as one or more singleton descriptors.
2. Call `AddCShells` and select the service type with `ShareSingletonWithShells<TService>()` (or the `Type` overload).
3. Activate two shell generations and resolve the service from each provider. Both resolutions should be reference-equal to the host resolution.
4. Dispose one shell generation while the other is still active. The shared object must remain usable and undisposed.
5. Dispose the root host. A root-created disposable shared object should be disposed exactly once by the root.
6. Repeat with `IEnumerable<TService>` and multiple singleton registrations to verify instance ordering and duplicate implementation registrations.
7. Verify a keyed registration of the same service type is still copied under its key and is not included in the selected unkeyed set.
8. Verify a feature registration added during shell configuration remains the last service returned for a single-service resolution.
9. Try a missing registration, a mixed-lifetime group, an open generic `Type`, and an excluded service type; each should fail with actionable context.
