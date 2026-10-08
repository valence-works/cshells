# Contract: CShellsBuilder host singleton sharing

## `ShareSingletonWithShells<TService>()`

Registers the closed service type `TService` for singleton sharing from the root host into every shell service provider.

## `ShareSingletonWithShells(Type serviceType)`

Registers a runtime-selected service type under the same contract. The argument must be non-null, closed, and not contain generic parameters.

## Selection and validation

- Sharing selects every unkeyed root descriptor whose `ServiceType` equals the selected type, in descriptor order.
- At least one matching unkeyed descriptor is required. Every matching unkeyed descriptor must use singleton lifetime.
- Keyed descriptors are not selected, resolved, or rewritten by this API.
- An alias registered under another service type is not implicitly selected.
- A closed generic selection cannot also match an unkeyed open-generic registration for its generic definition.
- The selected type cannot also be in the aggregated root-only shell exclusion set.
- Validation occurs when CShells copies the final root service collection into a shell provider. Invalid selections throw `InvalidOperationException` with the service type and corrective guidance.

## Ownership and resolution

- The shell sees the exact object returned by each corresponding root singleton registration, including when resolving `IEnumerable<TService>`.
- Instance descriptors in each shell keep shell-provider disposal from taking ownership of the shared objects.
- Root-created disposable singletons remain tracked by the root provider and are disposed there once. Caller-provided instances follow standard DI caller-ownership semantics.
- A selected factory that returns `null` is rejected with an actionable diagnostic.
- Registrations not selected retain existing copy semantics. Feature service registrations continue to be appended after root copies and therefore retain existing precedence.
- Shared registrations are borrowed root registrations, not forced-global resolutions. Later shell core or feature registrations retain their normal descriptor order and may override a selected registration for single-service resolution.
