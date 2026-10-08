# Data Model: Share Host Singletons with Shells

## Shared service selection

- **Identity**: One closed service `Type` selected by the host.
- **Validation**: Must match at least one unkeyed root service descriptor; every matching unkeyed descriptor must be a singleton; the service type must not be open or contain generic parameters; the type must not be in the shell-service exclusion set.
- A closed generic selection cannot coexist with an unkeyed open-generic registration for its generic definition. A selected factory must resolve to a non-null object.
- **Scope**: All matching unkeyed descriptors for that service type are selected as one set. Keyed descriptors and aliases are independent.

## Host singleton registration

- **Identity**: An ordered unkeyed descriptor in the root `IServiceCollection` for a selected service type.
- **Resolution**: The root provider resolves the selected service set once for each shell-provider build. Default DI returns the cached singleton instance for each descriptor.
- **Ownership**: The root provider retains disposal ownership for instances it creates from implementation types or factories. Caller-provided instances remain caller-owned according to default Microsoft DI semantics. Shell providers receive instance descriptors and do not own or dispose these objects.

## Shell generation

- **Identity**: One shell service provider built from the root descriptors and that shell's feature registrations.
- **Sharing relationship**: Each selected registration is exposed in shell resolution with the exact root instance. Feature registrations added later retain normal DI ordering/override behavior.
- Selection contributes borrowed root registrations and does not force them to win over registrations appended later by shell core or features.
- **Lifecycle**: Multiple active/retiring generations may refer to one host singleton; destroying a generation does not affect that singleton.
