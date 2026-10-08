# Contract: Per-delivery Nuplane integration options

## Scope

This is the behavioral contract for the existing optional `CShells.Nuplane` adapter. It adds no public API or configuration key. See [`NuplaneIntegrationOptions`](../../../src/CShells.Nuplane/NuplaneIntegrationOptions.cs) and the [adapter guide](../../../src/CShells.Nuplane/README.md) for public names and host examples.

## Supported options source

Supported configuration includes `WithNuplaneFeatureDiscovery(Action<NuplaneIntegrationOptions>)`, standard options configuration/binding, and dependency-aware `services.AddOptions<NuplaneIntegrationOptions>().Configure<TDependency>(...)`. A host with a custom options provider may register `IOptionsMonitor<NuplaneIntegrationOptions>` explicitly. Replacing only `IOptions<NuplaneIntegrationOptions>` is unsupported; the adapter does not combine monitor and static values or define precedence between them.

## Delivery boundary

For a delivered callback, null arguments, an already-canceled token, and ineligible empty work are checked first. Eligibility means at least one successfully applied package or at least one committed removal. These paths do not read the options monitor or mutate epochs or access the shell registry.

For an eligible callback, read `IOptionsMonitor<NuplaneIntegrationOptions>.CurrentValue` once. Before any epoch mutation, registry access, or await:

1. copy `Enabled`, `RefreshTrigger`, `AutoReload`, and the `OnReloadResults` delegate reference to one private immutable operation value;
2. verify `RefreshTrigger` is a defined enum member and reject an undefined value with an actionable configuration error;
3. allow options-monitor and options-validation failures to propagate before coordinator side effects; no new wrapper or last-known-good fallback is required.

All policy decisions and result reporting for the admitted delivery use these copied values. Changes after capture apply to later deliveries only. The synchronous field copy is not an atomic multi-property transaction against concurrent mutation of one options object.

## Field behavior

- `Enabled = false`: do not record new observer-requested work; retain existing freshness and reload epochs. Build-participant refreshes for previously recorded work are independent of this observer setting.
- `RefreshTrigger = ChangedOrPending`: preserve existing change and pending-freshness behavior.
- `RefreshTrigger = EveryEligibleCompletion`: preserve existing refresh behavior for every eligible delivery.
- `AutoReload = false`: freshness work may proceed, but do not attempt or acknowledge pending reload work. A later eligible delivery with reload enabled may retry retained work without rescanning an already-fresh catalog unless a newer package change requires refresh.
- `OnReloadResults`: invoke the callback captured by that delivery with the existing stable read-only copy of raw registry results. Preserve existing result evaluation, partial-error, cancellation, reentry, and retry behavior.

The build participant does not read these observer options. It consumes already-recorded freshness before the catalog read and does not activate, reload, or report a shell.

## Non-goals and limits

The contract does not add an options-change listener, timer, background retry loop, new public flag, new package dependency, core-package policy, host composition, stable pin, package-readability guarantee, physical unloading, pruning, or Foundation host readiness policy. Those remain with their existing owners and acceptance gates.
