# Research: Share Host Singletons with Shells

## Decision: Copy resolved host objects as shell instance registrations

- **Decision**: For each opted-in closed service type, validate the final root descriptor set and resolve its complete matching unkeyed registration set through the root provider. Add the returned objects to each shell's service collection as singleton instance descriptors in the same order.
- **Rationale**: The root provider owns singleton instances created from implementation types or factories. A shell descriptor whose factory returns one of those objects can cause the shell provider to capture and dispose that same object. Instance descriptors avoid transferring disposal ownership to each shell provider.
- **Alternatives considered**: Copy descriptors unchanged (creates one singleton per shell); use factories that delegate to root (risks child-provider disposal ownership); introduce an ownership wrapper (adds an abstraction and changes resolved object identity).

## Decision: Select by closed unkeyed service type, with generic and Type overloads

- **Decision**: Add `ShareSingletonWithShells<TService>()` and `ShareSingletonWithShells(Type serviceType)` to the existing `CShellsBuilder`. The generic method forwards to the Type overload. The Type overload permits runtime-selected contracts and allows explicit validation of open generic types.
- **Rationale**: Registration identity in Microsoft DI is the service type plus a descriptor entry. Selecting the entire unkeyed service-type group makes `IEnumerable<TService>` behavior predictable, while requiring explicit alias selections avoids implicit sharing of implementation aliases.
- **Alternatives considered**: Select registrations by implementation type (ambiguous for aliases and factories); add an individual-descriptor selector (not aligned with the agreed all-registration contract); generic-only overload (cannot testfully guard invalid open-generic Type input).

## Decision: Validate at shell-provider copy time

- **Decision**: Check missing registrations, keyed/unkeyed distinction, singleton lifetimes, open generic service types, and root-only exclusions when a shell provider is being built.
- **Rationale**: `AddCShells` runs before the final host registrations are necessarily complete. Build-time validation observes the final root service collection and exclusion registry, so it cannot falsely reject a selection registered after the builder call.
- **Alternatives considered**: Validate in `ShareSingletonWithShells` immediately (registration order becomes observable and later additions are mishandled); silently ignore excluded types (creates a successful but ineffective configuration).

## Decision: Do not enumerate keyed registrations for sharing

- **Decision**: Only descriptors with `IsKeyedService == false` are candidates. Keyed descriptors are copied through the pre-existing path without resolution by the sharing feature.
- **Rationale**: The requested API is a service-type-wide unkeyed selection; keyed registrations use a separate resolution contract and may have different key semantics.
- **Alternatives considered**: Share all keys matching the service type (not requested and would alter keyed behavior); reject any keyed entries (unnecessarily couples otherwise valid unkeyed sharing to keyed registrations).

## Decision: Reject inherited open-generic matches and null results explicitly

- **Decision**: If a selected closed generic service type also matches an unkeyed open-generic descriptor, reject the selection. Reject a root enumerable containing a null result before constructing shell instance descriptors.
- **Rationale**: A closed `GetServices<T>()` result can include instances produced by open-generic registrations that are not in the exact closed descriptor group. Silently pairing those objects with selected exact descriptors would break descriptor-order guarantees. Explicit null handling avoids leaking a lower-level descriptor-construction exception.
- **Alternatives considered**: Pair the result by count and rely on a confusing mismatch error; silently allow open-generic registrations; let null fail later during shell descriptor creation.
