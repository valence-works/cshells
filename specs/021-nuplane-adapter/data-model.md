# Data Model: Optional Nuplane Feature Discovery and Deferred Catalog Freshness

All state is private, process-local coordinator state. No package catalog, store entry, assembly reference, or persistent record is written by CShells.Nuplane.

## `NuplaneIntegrationOptions`

| Field | Type | Default | Meaning |
|---|---|---|---|
| `Enabled` | `bool` | `true` | Gates observer-driven reconciliation work. It does not gate the provider's normal initial query or claim generation protection. |
| `RefreshTrigger` | `NuplaneRefreshTrigger` | `ChangedOrPending` | Chooses whether every delivered eligible completion requests a refresh, or only package changes and retained freshness work do. |
| `AutoReload` | `bool` | `false` | Requests active-shell reload after successful catalog freshness work. Reload remains separate from refresh. |
| `OnReloadResults` | `Func<IReadOnlyList<ReloadResult>, CancellationToken, ValueTask>?` | `null` | Optional host-owned reporting callback receiving a stable, read-only snapshot of unmodified per-shell results. |

The options object is registered through standard `IOptions<NuplaneIntegrationOptions>` DI so a host can use `Configure<TDependency>` to bind a callback to host logging/refusal services.

## `NuplaneRefreshTrigger`

- `EveryEligibleCompletion`: request a refresh for each delivered `OnPackagesReconciled` callback, including unchanged change sets with successful applications.
- `ChangedOrPending`: request a refresh when the change set contains additions, updates, or removals, or when an earlier catalog refresh epoch is still outstanding. A callback with no changes and no pending freshness does not scan.

An eligible completion is delivered only when at least one package was successfully applied or a package removal committed. An empty applied list plus a nonempty `Removed` list is eligible. Quiet empty and failed-only cycles are not completions.

## Coordinator state

| State | Type | Advances when | Acknowledged when |
|---|---|---|---|
| `requestedCatalogEpoch` | `long` | A source change requires refresh, or the trigger is `EveryEligibleCompletion` for a delivered eligible callback. Increment happens before active-shell lookup. | Never directly; it remains greater than `committedCatalogEpoch` until a successful refresh captures it. |
| `committedCatalogEpoch` | `long` | The refresh gate captures and successfully refreshes an epoch. | Set to the captured epoch only. A newer epoch remains pending. |
| `requestedReloadEpoch` | `long` | A callback with active shells, `AutoReload` enabled, and catalog refresh work creates a new reload epoch. A later callback that only retries existing pending reload work captures that epoch without incrementing it. No-active callbacks create no new reload work. | Never directly; independent of catalog epoch. |
| `committedReloadEpoch` | `long` | Registry reload returns a nonempty result set with no per-shell errors and the host result callback completes. | Set to the captured reload epoch only. Newer requests and callback/error failures remain pending. |
| `epochStateLock` | `object` | Synchronizes request/commit epoch reads and writes. | Held only for short synchronous state changes; never across catalog, registry, or host callback awaits. |
| `refreshGate` | `SemaphoreSlim(1, 1)` | Acquired before checking/capturing catalog work. | Released before invoking `ReloadActiveAsync`. |
| `reloadGate` | `SemaphoreSlim(1, 1)` | Acquired when reload work is ready and at least one shell is active. | Released after result inspection and callback delivery. `refreshGate` is already released before the registry reload begins. |

The holder and coordinator are non-disposable and contain only managed epochs, semaphores, and root-service delegates. The build lease is a no-op and holds no assembly, catalog snapshot, or load-context reference.

## State transitions

1. An eligible callback first checks `Enabled`; if enabled, it determines whether the configured trigger requests a refresh and advances the request epoch under `epochStateLock` before reading active shells. The lock is released before any awaited work so a newer callback can record an epoch during refresh.
2. If no shell is active, observer work returns without catalog scan, activation, or reload. Requested catalog freshness remains available to a later real build; no reload epoch is created because that build will compose the new shell from the refreshed catalog. A reload epoch that was already pending stays pending but is not attempted or advanced until an active-shell eligible callback.
3. If a shell is active and refresh is requested, refresh captures its current epoch under `refreshGate` and `epochStateLock`. Success commits only that captured epoch under the state lock; failure or cancellation changes no committed epoch.
4. A requested build calls `BeginAsync` before feature catalog initialization/read. If requested catalog epoch exceeds committed epoch, the participant refreshes under the same gate and returns a no-op lease.
5. A newer event during refresh increments the request epoch under `epochStateLock`. The in-flight refresh cannot acknowledge it; a later eligible callback or requested build handles it.
6. Observer-driven auto reload creates a new reload epoch only when active shells exist, `AutoReload` is enabled, and the eligible callback requests catalog refresh work. A later eligible callback can retry existing reload work without incrementing its epoch when catalog freshness is already committed. An unchanged callback under `ChangedOrPending` with no catalog or reload work does no refresh or reload. A build-triggered refresh does not request reload because the build itself consumes the refreshed catalog. The coordinator releases `refreshGate` first, then reloads active shells under `reloadGate`.
7. Reload results are copied to a stable read-only list and delivered unchanged to `OnReloadResults` while the serialized auto-reload operation retains `reloadGate`. `refreshGate` is released before calling `ReloadActiveAsync`; a callback-triggered manual build/reload therefore cannot deadlock on that gate, and participant work does not acquire `reloadGate`. The callback is reporting-only and has no guarantee that reentrant Nuplane observer dispatch is supported. The coordinator inspects `Error` on every per-shell result independently. Partial errors, thrown/cancelled registry work, or callback faults leave reload epoch pending.
8. Successful reloads that occurred before callback failure are not rolled back. On a later callback they may be repeated; the coordinator does not claim exactly-once reload execution.
