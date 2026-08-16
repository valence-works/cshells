using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Lifecycle;

/// <summary>
/// Default <see cref="IShellRegistry"/> implementation. Holds the in-memory index of
/// <b>active</b> shell generations; delegates blueprint lookup and catalogue listing to the
/// single injected <see cref="IShellBlueprintProvider"/>.
/// </summary>
internal sealed class ShellRegistry : IShellRegistry
{
    private readonly ShellProviderBuilder? _providerBuilder;
    private readonly IServiceProvider? _rootProvider;
    private readonly IShellBlueprintProvider _blueprintProvider;
    private readonly ILogger<ShellRegistry> _logger;
    private readonly ConcurrentDictionary<string, NameSlot> _slots = new(StringComparer.OrdinalIgnoreCase);
    private ImmutableList<IShellLifecycleSubscriber> _subscribers = [];

    public ShellRegistry(
        IShellBlueprintProvider blueprintProvider,
        ShellProviderBuilder? providerBuilder = null,
        IServiceProvider? rootProvider = null,
        ILogger<ShellRegistry>? logger = null,
        IEnumerable<IShellLifecycleSubscriber>? subscribers = null)
    {
        _blueprintProvider = Guard.Against.Null(blueprintProvider);
        _providerBuilder = providerBuilder;
        _rootProvider = rootProvider;
        _logger = logger ?? NullLogger<ShellRegistry>.Instance;

        // Subscribers registered in DI are subscribed at construction time so they observe
        // every transition — including the first activation kicked off by the startup hosted
        // service. Without this, factory-based registrations (e.g., AddSingleton<…>(sp => …))
        // would only materialize on the first GetServices<IShellLifecycleSubscriber>() call,
        // which never happens in the normal flow.
        if (subscribers is not null)
        {
            foreach (var subscriber in subscribers)
                Subscribe(subscriber);
        }
    }

    // Convenience ctor used by tests that don't need the provider-build pipeline.
    internal ShellRegistry(IShellBlueprintProvider blueprintProvider, ILogger<ShellRegistry>? logger)
        : this(blueprintProvider, providerBuilder: null, rootProvider: null, logger, subscribers: null)
    {
    }

    // =========================================================================
    // Activation
    // =========================================================================

    /// <inheritdoc />
    public async Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(name);
        EnsureProviderBuilder();

        var slot = _slots.GetOrAdd(name, static _ => new NameSlot());

        // Fast path: active shell already published. Volatile read via field.
        if (slot.Active is { } existing)
            return existing;

        await slot.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-check under the semaphore: a concurrent caller may have activated in the
            // meantime. This is the stampede-safety guarantee — exactly one build per name.
            if (slot.Active is { } alreadyActive)
                return alreadyActive;

            var blueprint = await LookupBlueprintAsync(name, wrapFault: true, cancellationToken).ConfigureAwait(false)
                ?? throw new ShellBlueprintNotFoundException(name);

            return await CreateGenerationAsync(slot, blueprint.Blueprint, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            slot.Semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IShell> ActivateAsync(string name, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(name);
        EnsureProviderBuilder();

        var slot = _slots.GetOrAdd(name, static _ => new NameSlot());

        await slot.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (slot.Active is not null)
                throw new InvalidOperationException(
                    $"Shell '{name}' already has an Active generation (generation {slot.Active.Descriptor.Generation}). Use ReloadAsync to roll over or GetOrActivateAsync for idempotent access.");

            var blueprint = await LookupBlueprintAsync(name, wrapFault: true, cancellationToken).ConfigureAwait(false)
                ?? throw new ShellBlueprintNotFoundException(name);

            return await CreateGenerationAsync(slot, blueprint.Blueprint, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            slot.Semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ReloadResult> ReloadAsync(string name, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(name);
        EnsureProviderBuilder();

        var slot = _slots.GetOrAdd(name, static _ => new NameSlot());

        Shell? previousActive = null;
        IShell? newShell = null;
        Exception? error = null;

        // Not-found is a caller-programming error and propagates eagerly — callers expect to
        // know immediately that a reload target is unknown. Composition/build/initializer
        // failures are captured into ReloadResult.Error so ReloadActiveAsync can continue the
        // batch past a transient single-shell fault.
        var provided = await LookupBlueprintAsync(name, wrapFault: true, cancellationToken).ConfigureAwait(false)
            ?? throw new ShellBlueprintNotFoundException(name);

        await slot.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            previousActive = slot.Active;

            try
            {
                newShell = await CreateGenerationAsync(slot, provided.Blueprint, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Current active generation is unaffected; no partial entry retained.
                error = ex;
            }

            if (newShell is not null && previousActive is not null)
            {
                // Promote the new generation by transitioning the old one to Deactivating. Drain
                // runs outside the semaphore so a slow drain doesn't block the next reload.
                await previousActive.ForceAdvanceAsync(ShellLifecycleState.Deactivating).ConfigureAwait(false);
            }
        }
        finally
        {
            slot.Semaphore.Release();
        }

        if (error is not null)
            return new ReloadResult(name, NewShell: null, Drain: null, Error: error);

        IDrainOperation? drainOp = null;
        if (previousActive is not null)
            drainOp = await DrainAsync(previousActive, cancellationToken).ConfigureAwait(false);

        return new ReloadResult(name, NewShell: newShell, Drain: drainOp, Error: null);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReloadResult>> ReloadActiveAsync(
        ReloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var opts = options ?? new ReloadOptions();
        opts.EnsureValid();

        var activeNames = _slots
            .Where(kv => kv.Value.Active is not null)
            .Select(kv => kv.Key)
            .ToList();

        if (activeNames.Count == 0)
            return [];

        var results = new ConcurrentBag<ReloadResult>();
        await Parallel.ForEachAsync(
            activeNames,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = opts.MaxDegreeOfParallelism,
                CancellationToken = cancellationToken
            },
            async (name, ct) =>
            {
                try
                {
                    results.Add(await ReloadAsync(name, ct).ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    results.Add(new ReloadResult(name, NewShell: null, Drain: null, Error: ex));
                }
            }).ConfigureAwait(false);

        return results.ToList();
    }

    // =========================================================================
    // Unregister
    // =========================================================================

    /// <inheritdoc />
    public async Task UnregisterBlueprintAsync(string name, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(name);

        // Phase 0: resolve the blueprint + its owning manager via the provider. Raw propagation
        // here (no wrap into ShellBlueprintUnavailableException) — unregister is an admin flow
        // and callers want the original fault for diagnostics.
        var provided = await _blueprintProvider.GetAsync(name, cancellationToken).ConfigureAwait(false)
            ?? throw new ShellBlueprintNotFoundException(name);

        if (provided.Manager is null)
            throw new BlueprintNotMutableException(name);

        // Phase 1: persist the delete. Propagates manager exceptions raw.
        await provided.Manager.DeleteAsync(name, cancellationToken).ConfigureAwait(false);

        // Phase 2: drain + remove in-memory slot. Serializes against any in-flight activation
        // for this name via the slot's semaphore.
        if (!_slots.TryGetValue(name, out var slot))
            return;  // Nothing active; persistent state was cleaned and nothing else to do.

        Shell? activeToDrain;
        await slot.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            activeToDrain = slot.Active;
            slot.Active = null;
        }
        finally
        {
            slot.Semaphore.Release();
        }

        if (activeToDrain is not null)
        {
            var drainOp = await DrainAsync(activeToDrain, cancellationToken).ConfigureAwait(false);
            await drainOp.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        // Remove the slot entirely so repeated unregister + re-create cycles don't leave
        // stranded semaphores in the dict. `_slots.TryRemove(name, out _)` is safe even under
        // a concurrent `GetOrActivateAsync` for the same name — the re-create path will
        // allocate a fresh slot.
        _slots.TryRemove(name, out _);
    }

    // =========================================================================
    // Read access
    // =========================================================================

    /// <inheritdoc />
    public Task<ProvidedBlueprint?> GetBlueprintAsync(string name, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(name);
        return _blueprintProvider.GetAsync(name, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IShellBlueprintManager?> GetManagerAsync(string name, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(name);
        var provided = await _blueprintProvider.GetAsync(name, cancellationToken).ConfigureAwait(false);
        return provided?.Manager;
    }

    /// <inheritdoc />
    public async Task<ShellPage> ListAsync(ShellListQuery query, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(query);
        query.EnsureValid();

        var catalogue = await _blueprintProvider.ListAsync(
            new BlueprintListQuery(query.Cursor, query.Limit, query.NamePrefix),
            cancellationToken).ConfigureAwait(false);

        var items = catalogue.Items
            .Select(summary => BuildShellSummary(summary))
            .Where(summary => query.StateFilter is null || summary.State == query.StateFilter)
            .ToList();

        return new ShellPage(items, catalogue.NextCursor);
    }

    private ShellSummary BuildShellSummary(BlueprintSummary summary)
    {
        if (!_slots.TryGetValue(summary.Name, out var slot) || slot.Active is not { } active)
        {
            return new ShellSummary(
                summary.Name,
                summary.SourceId,
                summary.Mutable,
                ActiveGeneration: null,
                State: null,
                ActiveScopeCount: 0,
                LastScopeOpenedAt: null,
                summary.Metadata);
        }

        return new ShellSummary(
            summary.Name,
            summary.SourceId,
            summary.Mutable,
            ActiveGeneration: active.Descriptor.Generation,
            State: active.State,
            ActiveScopeCount: active.ActiveScopeCount,
            LastScopeOpenedAt: null,  // populated in feature 008 when LastScopeOpenedAt lands on Shell
            summary.Metadata);
    }

    /// <inheritdoc />
    public IShell? GetActive(string name)
    {
        Guard.Against.NullOrWhiteSpace(name);
        return _slots.TryGetValue(name, out var slot) ? slot.Active : null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IShell> GetAll(string name)
    {
        Guard.Against.NullOrWhiteSpace(name);
        return _slots.TryGetValue(name, out var slot) ? slot.All : [];
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IShell> GetActiveShells() =>
        _slots.Values.Select(s => s.Active).OfType<IShell>().ToList();

    // =========================================================================
    // Drain
    // =========================================================================

    /// <inheritdoc />
    public Task<IDrainOperation> DrainAsync(IShell shell, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(shell);

        if (shell is not Shell typedShell)
            throw new ArgumentException(
                $"DrainAsync only accepts shells produced by this registry (CShells.Lifecycle.Shell); got {shell.GetType().FullName}.",
                nameof(shell));

        // Idempotent: the first caller CAS-publishes the new DrainOperation onto the Shell;
        // concurrent callers observe the published instance and return early. This preserves
        // the IDrainOperation contract ("concurrent callers for the same shell receive the
        // same instance") with one less moving part than the previous Lazy<T>+ConcurrentDictionary
        // pattern — the drain reference now lives on the Shell where it always belonged.
        if (typedShell.Drain is { } existing)
            return Task.FromResult(existing);

        var policy = ResolveDrainPolicy();
        var gracePeriod = ResolveGracePeriod();
        var candidate = new DrainOperation(typedShell, policy, gracePeriod, ResolveDrainLogger());

        var winner = typedShell.PublishDrain(candidate);
        if (ReferenceEquals(winner, candidate))
            StartDrainRun(typedShell, candidate);

        return Task.FromResult<IDrainOperation>(winner);
    }

    private static void StartDrainRun(Shell shell, DrainOperation op)
    {
        // Transition to Draining (Active or Deactivating → Draining). Best-effort CAS.
        _ = shell.ForceAdvanceAsync(ShellLifecycleState.Draining);

        // Run the drain in the background. The Shell holds the reference to op via Drain;
        // both become GC-eligible together once the registry releases the slot's reference
        // to the Shell.
        _ = op.RunAsync();
    }

    private IDrainPolicy ResolveDrainPolicy()
    {
        if (_rootProvider is null)
            return new Policies.FixedTimeoutDrainPolicy(TimeSpan.FromSeconds(30));

        return _rootProvider.GetService<IDrainPolicy>()
               ?? new Policies.FixedTimeoutDrainPolicy(TimeSpan.FromSeconds(30));
    }

    private TimeSpan ResolveGracePeriod() =>
        _rootProvider?.GetService<DrainGracePeriod>()?.Value ?? TimeSpan.FromSeconds(3);

    private ILogger<DrainOperation>? ResolveDrainLogger() =>
        _rootProvider?.GetService<ILogger<DrainOperation>>();

    // =========================================================================
    // Subscribers
    // =========================================================================

    /// <inheritdoc />
    public void Subscribe(IShellLifecycleSubscriber subscriber)
    {
        Guard.Against.Null(subscriber);
        ImmutableInterlocked.Update(ref _subscribers,
            static (list, s) => list.Contains(s) ? list : list.Add(s),
            subscriber);
    }

    /// <inheritdoc />
    public void Unsubscribe(IShellLifecycleSubscriber subscriber)
    {
        Guard.Against.Null(subscriber);
        ImmutableInterlocked.Update(ref _subscribers,
            static (list, s) => list.Remove(s),
            subscriber);
    }

    /// <summary>
    /// Fans out a state-change event to every registered subscriber. Subscriber exceptions are
    /// caught and logged so one failing subscriber cannot block peers or the transition.
    /// </summary>
    internal async Task FireStateChangedAsync(
        IShell shell,
        ShellLifecycleState previous,
        ShellLifecycleState current,
        CancellationToken cancellationToken = default)
    {
        var snapshot = _subscribers;
        if (snapshot.IsEmpty)
            return;

        var activationFailure = (ShellGenerationActivationException?)null;

        foreach (var subscriber in snapshot)
        {
            try
            {
                await subscriber.OnStateChangedAsync(shell, previous, current, cancellationToken).ConfigureAwait(false);
            }
            catch (ShellGenerationActivationException ex)
            {
                // Candidate publication is the one subscriber failure that must abort the
                // generation. Continue notifying peers first so subscriber isolation remains
                // intact, then let the transition owner dispose the rejected candidate.
                activationFailure ??= ex;
                _logger.LogError(ex,
                    "Shell generation publication failed in subscriber {SubscriberType} during {Previous} → {Current} for shell {Shell}",
                    subscriber.GetType().FullName, previous, current, shell.Descriptor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Shell lifecycle subscriber {SubscriberType} threw during {Previous} → {Current} for shell {Shell}",
                subscriber.GetType().FullName, previous, current, shell.Descriptor);
            }
        }

        if (activationFailure is not null)
            throw activationFailure;
    }

    // =========================================================================
    // Internals
    // =========================================================================

    /// <summary>
    /// Resolves a blueprint from the host's single <see cref="IShellBlueprintProvider"/>,
    /// optionally wrapping provider faults in <see cref="ShellBlueprintUnavailableException"/>.
    /// Activation entry points wrap; the public <see cref="GetBlueprintAsync"/> and
    /// <see cref="UnregisterBlueprintAsync"/> paths do NOT wrap (they want the raw fault for
    /// diagnostics).
    /// </summary>
    private async Task<ProvidedBlueprint?> LookupBlueprintAsync(string name, bool wrapFault, CancellationToken cancellationToken)
    {
        try
        {
            return await _blueprintProvider.GetAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (wrapFault && ShouldWrapAsUnavailable(ex))
        {
            throw new ShellBlueprintUnavailableException(name, ex);
        }
    }

    /// <summary>
    /// Decides whether a provider exception should be wrapped as
    /// <see cref="ShellBlueprintUnavailableException"/> (→ HTTP 503) or propagated as-is.
    /// Structured signals (not-found, cancellation) are deterministic and should NOT be
    /// masked as transient "unavailable".
    /// </summary>
    private static bool ShouldWrapAsUnavailable(Exception ex) =>
        ex is not ShellBlueprintNotFoundException &&
        ex is not OperationCanceledException;

    private void EnsureProviderBuilder()
    {
        if (_providerBuilder is null)
            throw new InvalidOperationException(
                "Registry was constructed without a ShellProviderBuilder. Use AddCShells(...) to configure the container.");
    }

    /// <summary>
    /// Compose → build → initialize → promote. Must be called under the name's semaphore.
    /// </summary>
    private async Task<IShell> CreateGenerationAsync(NameSlot slot, IShellBlueprint blueprint, CancellationToken cancellationToken)
    {
        // Assign the generation number. If the rest of this method throws we simply "skip" this
        // number; the next successful reload picks up the following value. This satisfies
        // no-reuse and no-partial-entry without bookkeeping.
        var generation = Interlocked.Increment(ref slot.NextGeneration);

        var settings = await blueprint.ComposeAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(settings.Id.Name, blueprint.Name, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Blueprint '{blueprint.Name}' produced settings with Id.Name '{settings.Id.Name}' — blueprint name mismatch.");

        var buildResult = await _providerBuilder!.BuildAsync(settings, cancellationToken).ConfigureAwait(false);

        var descriptor = ShellDescriptor.Create(blueprint.Name, (int)generation, blueprint.Metadata);
        var shell = new Shell(descriptor, buildResult.Provider, async (s, prev, curr) =>
        {
            await FireStateChangedAsync(s, prev, curr).ConfigureAwait(false);

            // A generation transitioning to Disposed has had its endpoints/middleware removed (the
            // Disposed-keyed subscriber cleanup ran just above, since FireStateChangedAsync is awaited
            // first), and its IServiceProvider teardown is in flight on this same call stack (Shell
            // advances to Disposed before disposing the provider). Release the registry's last strong
            // reference to it now so the Shell — and, transitively, its (being-)disposed provider, which
            // still holds the generation's service *types* — becomes GC-eligible once that teardown
            // completes. Without this, slot.All pins every generation ever created for the lifetime of the
            // host: an unbounded leak, and — when a generation's assemblies were loaded into a collectible
            // AssemblyLoadContext — the disposed provider's type references keep that context (and its
            // assemblies) resident forever, so it can never be unloaded.
            if (curr == ShellLifecycleState.Disposed)
                ReleaseGeneration(slot, s);
        });

        // Populate the holder so services in the shell's provider can resolve IShell.
        buildResult.Holder.Set(shell);

        try
        {
            await RunInitializersAsync(descriptor, buildResult.Provider, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisposePartialProviderAsync(buildResult.Provider).ConfigureAwait(false);
            throw;
        }

        try
        {
            if (!await shell.TryTransitionAsync(ShellLifecycleState.Initializing, ShellLifecycleState.Active).ConfigureAwait(false))
                throw new InvalidOperationException("Shell failed to transition from Initializing to Active.");
        }
        catch
        {
            // A publication subscriber can reject the candidate after the state CAS has moved it
            // to Active. Dispose that unpublished generation before surfacing the failure so its
            // provider, middleware, and collectible feature assemblies are not leaked.
            await shell.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        slot.Active = shell;
        // Atomic add: paired with the lock-free removal in ReleaseGeneration, which runs off the
        // drain background thread (outside this name's semaphore) when a generation reaches Disposed.
        ImmutableInterlocked.Update(ref slot.All, static (list, s) => list.Add(s), shell);

        // Safe-by-construction guard against an add-after-release: if this generation somehow already
        // reached Disposed before the add above (unreachable today — a drained initializer fails the
        // Initializing→Active CAS at TryTransitionAsync and throws before we get here), its Disposed
        // callback's Remove ran against a list that did not yet contain it and was a no-op, so the add
        // just re-inserted a disposed shell. Re-run the release now rather than leak it back in.
        if (shell.State == ShellLifecycleState.Disposed)
            ReleaseGeneration(slot, shell);

        _logger.LogInformation("Activated shell {Descriptor} with {FeatureCount} feature(s)",
            descriptor, buildResult.EnabledFeatures.Count);

        return shell;
    }

    private async Task RunInitializersAsync(ShellDescriptor descriptor, IServiceProvider provider, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var initializers = scope.ServiceProvider.GetServices<IShellInitializer>().ToList();
        var registrations = scope.ServiceProvider.GetServices<ShellInitializerRegistration>().ToList();
        if (initializers.Count == 0 && registrations.Count == 0)
            return;

        var planner = new ShellInitializerOrderingPlanner();
        var plan = planner.Plan(descriptor, initializers, registrations);

        foreach (var diagnostic in plan.Diagnostics)
        {
            _logger.LogDebug(
                "{Message} Shell: {Shell}. Initializers: {Initializers}",
                diagnostic.Message,
                descriptor,
                string.Join(", ", diagnostic.InitializerTypes.Select(t => t.FullName ?? t.Name)));
        }

        foreach (var entry in plan.Entries)
        {
            await entry.Initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Removes a fully-disposed generation from its slot's historical list so it stops being rooted
    /// by the registry, and — when that generation is still the published <see cref="NameSlot.Active"/>
    /// one — clears Active too, preserving the invariant that <c>GetAll</c> always contains
    /// <c>GetActive</c>. Lock-free (runs from the drain background thread, not under the slot semaphore);
    /// the paired add in <see cref="CreateGenerationAsync"/> is also atomic. Idempotent — removing a
    /// shell not present is a no-op.
    /// </summary>
    private static void ReleaseGeneration(NameSlot slot, IShell shell)
    {
        if (shell is not Shell typed)
            return;

        ImmutableInterlocked.Update(ref slot.All, static (list, s) => list.Remove(s), typed);

        // Preserve GetAll ⊇ {GetActive}: if the generation being released is still the active one — an
        // active generation drained without a reload replacing it, reachable via the public DrainAsync and
        // on host shutdown — clear Active too, so GetActive never returns a Disposed shell that GetAll no
        // longer holds. Atomic CAS: a concurrent reload that already promoted a newer generation into
        // Active leaves it untouched (the stored reference no longer matches `typed`).
#pragma warning disable 420 // Interlocked provides the fence; other volatile reads/writes of Active are unaffected.
        Interlocked.CompareExchange(ref slot.Active, null, typed);
#pragma warning restore 420
    }

    private static async ValueTask DisposePartialProviderAsync(ServiceProvider provider)
    {
        try
        {
            await provider.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Partial-provider disposal failures are swallowed — the primary exception already
            // propagates, and tearing down a half-built container sometimes throws benignly.
        }
    }

    /// <summary>
    /// Per-name state: a serialization semaphore for activate/reload/unregister, a generation
    /// counter, and the currently-active + historical shells. The catalogue blueprint itself
    /// is NOT held here — it lives in the provider and is fetched on every activation.
    /// </summary>
    private sealed class NameSlot
    {
        internal readonly SemaphoreSlim Semaphore = new(1, 1);

        // Incremented under the Semaphore. `long` so the cast to int in ShellDescriptor is explicit.
        internal long NextGeneration;

        // Written under the Semaphore, plus a lock-free CAS-to-null in ReleaseGeneration when the active
        // generation is disposed; read without locking by GetActive.
        internal volatile Shell? Active;

        // Immutable list. Added to under the Semaphore; removed from lock-free on the drain thread
        // (ReleaseGeneration) — both via ImmutableInterlocked.Update.
        internal ImmutableList<Shell> All = [];
    }
}
