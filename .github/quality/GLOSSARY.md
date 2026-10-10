# Glossary

The ubiquitous language of CShells: one word per concept, one concept per word.

This is the **target** vocabulary. The Status column says where each term stands:

- **keep**: the canonical term. Use it in code, docs, logs and commit titles.
- **rename** / **merge** / **retire**: the term is on its way out. The open `audit:*` issues listed in #209 track the work. Don't introduce it in new code; when you touch code that uses it, prefer the replacement in Notes.

A PR that adds public vocabulary adds it here and retires any synonym in the same PR.

| Term | Definition | Status | Notes |
|---|---|---|---|
| Shell | A named, isolated set of enabled features with its own DI container, typically one per tenant. | keep | "Tenant" is a use-case word for docs only; never in type names. |
| Shell name | The case-insensitive string that identifies a shell across blueprints, registry, routing and logs. | keep | The canonical identity. The log field is `{ShellName}`. |
| ShellId | A struct wrapper around the shell name. | retire | Replace with `string` plus a shared `OrdinalIgnoreCase` comparer. |
| Shell generation (IShell) | One immutable build of a shell (service provider plus state), numbered 1, 2, 3… per shell name. | keep | "Generation" is reserved for this sense only and is typed `int` everywhere. |
| Shell descriptor | The identity of a generation: name, generation number, creation time, metadata; formatted `name#gen`. | keep | The log field is `{Shell}`. |
| Blueprint | A re-invocable recipe that composes a shell's settings each time the shell is activated or reloaded. | keep | |
| Compose | Asking a blueprint to produce fresh shell settings. | keep | |
| Blueprint provider | The single host-registered source that looks up and lists blueprints by shell name. | keep | Replaces "shell settings provider" and "configuration provider" (`WithConfigurationProvider` becomes `WithConfigurationBlueprints`). |
| Blueprint manager | An optional writer that persists create, update and delete for a mutable blueprint source. | keep | A blueprint is mutable if and only if it has a manager. |
| Shell settings | The composed definition of a shell: name, enabled/disabled features, shell configuration. | keep | `ShellConfig` remains only as the JSON binding shape. |
| Shell configuration | The per-shell key/value tree surfaced as `IConfiguration` inside the shell. | keep | |
| Feature configuration | The subtree of shell configuration owned by one feature and bound into its options. | keep | Preferred over "feature settings". Rename the public members only when they are touched for other reasons. |
| Feature options (TOptions) | The typed object an `IConfigurableFeature<TOptions>` receives. | keep | The single feature-configuration model. Use "options" only for this type parameter. |
| Shell defaults (ConfigureAllShells) | Configuration applied to every shell before its own blueprint values. | rename | Rename the internal `ConfiguredShellBlueprintProvider` to `ShellDefaultsBlueprintProvider`. |
| Settings preparer | A host hook that patches shell configuration after dependency expansion, before features bind. | retire | Delete unless a downstream consumer is confirmed; if kept, rename it `IShellConfigurationPatcher`. |
| Feature | A class implementing `IShellFeature` that registers services (and optionally endpoints or middleware) into a shell. | keep | |
| Feature name | The unique key of a feature: `[ShellFeature("Name")]`, or the class name with any `ShellFeature`/`Feature` suffix removed. | keep | Merge "feature id" into this: drop `Id`, the `Name => Id` alias and the `{FeatureId}` log field. |
| Feature type | The CLR type implementing a feature. | rename | Replaces "startup type". |
| Feature catalog | The host-wide, refreshable set of features discovered from feature assemblies. | rename | Drop the "Runtime" prefix: `IFeatureCatalog`. |
| Catalog snapshot | An immutable view of the feature catalog at one refresh, identified by `Version`. | merge | Merge `RuntimeFeatureCatalogSnapshot` and `IRuntimeFeatureCatalogSnapshot`. Its `Generation` property becomes `Version`. |
| Catalog refresh | Recomputing and publishing a new catalog snapshot. | rename | Replaces "catalog commit" (event `Refreshed`). |
| Feature descriptor | Metadata about one discovered feature: name, display name, description, dependencies, metadata, type. | merge | Merge `ShellFeatureDescriptor` and `RuntimeFeatureDescriptor`. |
| Feature assembly provider | A source of assemblies scanned for features. | keep | |
| Host assemblies | The default set of feature assemblies discovered from the host's dependency context. | keep | Always included unless the host calls `ClearAssemblies()`. |
| Shared assemblies | A filter over host-assembly discovery. | retire | Has opposite meanings depending on mode. Delete, or rename to `FeatureAssemblyFilter` with a single meaning. |
| Shared singletons | Root singletons deliberately shared between the host and all shells (`ShareSingletonWithShells`). | keep | |
| Root services / service exclusion | Host DI registrations copied into each shell, minus the CShells infrastructure that must stay in the root. | keep | |
| Shell registry | The in-memory index of live generations; it activates, reloads, drains and unregisters them. | keep | |
| Activation | Building and initializing a new generation and making it the active one for its shell name. | keep | The only public word for this moment. |
| Active | The single committed, routable generation for a shell name. | keep | Redefined so it implies committed; the uncommitted window is `Initializing`. |
| Settled | An active generation whose activation has committed. | retire | Merge into Active; delete `ISettledShellRegistry`. |
| Commit / committed | The internal point at which an activating generation becomes authoritative. | retire | Internal only. |
| Publish / publication / promote | Making a generation or its endpoints visible. | retire | Use "activate"/"active". Internal endpoint code may keep "publish". |
| Candidate / provisional | A generation being activated but not yet active. | retire | Internal only. |
| Verified / returned generation | The generation reported by an activation-runner attempt. | retire | Disappears with the runner; if the runner is kept elsewhere, use a single `Generation` (int). |
| Activation runner | An opt-in service that activates a list of shells with retries. | retire | Moves out of core (into the consumer, or an opt-in package). |
| Activation participant | A two-phase-commit hook (Prepare/Commit/Complete/Rollback) around activation. | retire | Replaced by a single slot-swap commit. |
| Build participant / build lease | A hook holding a resource for the lifetime of one generation. | retire | Its only implementation is a no-op; replace with a catalog "mark stale" call. |
| Reload | Activating a new generation from a freshly composed blueprint and draining the previous one. | keep | Rename the management route `reload-all` to `reload-active`. |
| Unregister | Deleting a blueprint through its manager, then draining and disposing its generations. | keep | |
| Drain | Retiring a generation: wait for scopes, run teardown hooks, dispose, all within a policy-bounded deadline. | keep | |
| Drain policy / grace period | The rules for drain deadlines and the extra time allowed after a deadline or a force. | keep | Defaults are defined once (`FixedTimeoutDrainPolicy.Default`, `DrainGracePeriod.Default`). |
| Drain handler | A parallel teardown hook that runs while a generation is Draining (scopes may still be active). | keep | Distinct timing from terminators; do not merge. |
| Shell terminator | An ordered teardown hook that runs before a generation's provider is disposed. | keep | |
| Shell initializer | An ordered startup hook that runs inside a new generation before it becomes active. | keep | |
| Lifecycle phase / order | The coarse phase (Prepare/Default/Start) and numeric order that sort initializers and, mirrored, terminators. | keep | One shared `LifecycleRegistration` and one shared `LifecycleOrderException`. |
| Lifecycle subscriber | An observer, registered through DI, notified of every generation state transition. | keep | The only subscription mechanism (registry `Subscribe` is removed). |
| Post-configure shell services | A feature hook that runs after all features register services, before the provider is built. | keep | |
| Scope (IShellScope) | A tracked DI scope inside a generation; it delays drain until disposed. | keep | |
| Shell resolver | One ordered step that maps an incoming HTTP request to a shell name. | merge | Merge `IShellResolver` and `IShellResolverStrategy` into one interface; the composite becomes the internal `ShellResolverPipeline`. |
| Resolver pipeline | The ordered list of shell resolvers; list order is execution order. | keep | Replaces `ResolverOrderAttribute`, `SetOrder` and the `order:` arguments. |
| Fallback shell resolver | The last-resort resolver that picks the `Default` shell, else the first active shell. | rename | Replaces `DefaultShellResolverStrategy`, because "Default" is ambiguous. |
| Web routing | Host-wide shell resolution by path segment, host, header or claim. | keep | Configured only on `CShellsBuilder`. `WebRoutingShellResolverOptions` becomes `WebRoutingOptions`. |
| Shell address | The path, host, header value or claim value that selects one shell. | rename | Replaces per-shell `WebRoutingShellOptions` and `ShellBuilder.WithWebRouting`. "Route index" means the shell address index. |
| Shell resolution context | A string-keyed property bag passed to resolvers. | retire | Resolvers receive `HttpContext` directly. |
| Notification | The removed in-process pub/sub mechanism. | retire | Delete `CShells.Notifications`; use "lifecycle subscriber". |
| Shell property | The removed shell property bag and its serializer. | retire | Delete `IShellPropertySerializer` and `ShellConfigurationKeys`. |
| Shell initialization waiter | A never-signaled "all shells initialized" gate in `CShells.AspNetCore.Testing`. | retire | Delete the package. "Initialization" means `IShellInitializer` only. |
| Applied / desired / deferred shell; shell settings provider; shell manager; shell host | Vocabulary of removed architectures. | retire | Purge from docs and agent files. |
| Qualification | The spec-process record that a preview build was validated. | retire | Do not use it in code, test names or user-facing commit titles. |
| Handler | Overloaded: drain hooks, endpoint subscriber, notification handlers, Management API endpoints. | retire | Use "handler" only for Management API endpoint handlers and the `IDrainHandler` contract. |
| Provider | Blueprint source, assembly source, `IServiceProvider`, auth scheme source. | keep | Always qualified (blueprint provider, assembly provider, service provider). |
