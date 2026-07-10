using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;
using CShells.Management.Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Integration.Management;

/// <summary>
/// Integration tests for <c>POST /{name}/force-drain</c> (US4). Covers single in-flight
/// drain, two simultaneously-draining generations, the no-in-flight 404, the unknown-name
/// 404, and the only-Drained edge case.
/// </summary>
public class ForceDrainEndpointTests
{
    [Fact(DisplayName = "Force-drain with one in-flight generation returns 200 with a single result")]
    public async Task ForceDrain_OneInflightGeneration_Returns200_WithSingleResult_StatusForcedOrCompleted()
    {
        await using var fixture = new ManagementApiFixture(c => c
            .WithAssemblyContaining<ForceDrainEndpointTests>()
            .AddShell("acme", s => s.WithFeature<MgmtStuckDrainFeature>()));
        await fixture.Registry.GetOrActivateAsync("acme");

        // Reload once to leave one generation draining.
        await fixture.PostAsync("/admin/reload/acme");

        var response = await fixture.PostAsync("/admin/acme/force-drain");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DrainResultResponse[]>(WebJson);
        Assert.NotNull(body);
        Assert.Single(body);
        Assert.Contains(body[0].Status, new[] { "Forced", "Completed" });
    }

    [Fact(DisplayName = "Force-drain with two in-flight generations returns 200 with two results")]
    public async Task ForceDrain_TwoInflightGenerations_Returns200_WithTwoResults()
    {
        await using var fixture = new ManagementApiFixture(c => c
            .WithAssemblyContaining<ForceDrainEndpointTests>()
            .AddShell("acme", s => s.WithFeature<MgmtStuckDrainFeature>()));
        await fixture.Registry.GetOrActivateAsync("acme");

        // Two consecutive reloads → gen 1 and gen 2 both draining; gen 3 is active.
        await fixture.PostAsync("/admin/reload/acme");
        await fixture.PostAsync("/admin/reload/acme");

        var response = await fixture.PostAsync("/admin/acme/force-drain");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DrainResultResponse[]>(WebJson);
        Assert.NotNull(body);
        Assert.Equal(2, body.Length);
        Assert.All(body, entry => Assert.Contains(entry.Status, new[] { "Forced", "Completed" }));
    }

    [Fact(DisplayName = "Force-drain with no in-flight drain returns 404")]
    public async Task ForceDrain_NoInflightDrain_Returns404()
    {
        await using var fixture = new ManagementApiFixture(c => c
            .AddShell("acme", _ => { }));
        await fixture.Registry.GetOrActivateAsync("acme");

        var response = await fixture.PostAsync("/admin/acme/force-drain");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(WebJson);
        Assert.Contains("no in-flight drain", problem.GetProperty("detail").GetString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Force-drain of an unknown shell name returns 404")]
    public async Task ForceDrain_UnknownName_Returns404()
    {
        await using var fixture = new ManagementApiFixture(c => c.AddShell("known", _ => { }));

        var response = await fixture.PostAsync("/admin/does-not-exist/force-drain");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "Force-drain proceeds when generations exist even if blueprint provider is unavailable (Greptile PR-91 P1)")]
    public async Task ForceDrain_BlueprintProviderTransientlyUnavailable_StillForcesInflightDrains()
    {
        var stub = new ProviderWithGetSwitch();
        stub.AddBlueprint("acme", configure: s => s.WithFeature<MgmtStuckDrainFeature>());

        await using var fixture = new ManagementApiFixture(c => c
            .WithAssemblyContaining<ForceDrainEndpointTests>()
            .AddBlueprintProvider(_ => stub));

        await fixture.Registry.GetOrActivateAsync("acme");
        await fixture.PostAsync("/admin/reload/acme");

        // Now break the blueprint provider — force-drain MUST still succeed because the
        // shell's identity is already established by the in-memory registry; force-drain is
        // an emergency operation and shouldn't be gated on the blueprint store's health.
        stub.ThrowOnGet = new ApplicationException("blueprint store transiently unavailable");

        var response = await fixture.PostAsync("/admin/acme/force-drain");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DrainResultResponse[]>(WebJson);
        Assert.NotNull(body);
        Assert.Single(body);
    }

    [Fact(DisplayName = "Force-drain when only Drained generations remain returns 404")]
    public async Task ForceDrain_OnlyDrainedGenerations_Returns404()
    {
        await using var fixture = new ManagementApiFixture(c => c
            .AddShell("acme", _ => { })); // No drain handler — drain completes immediately.
        await fixture.Registry.GetOrActivateAsync("acme");

        // Reload completes drain immediately; old gen advances all the way to Disposed.
        // Wait via the actual drain operation rather than a timing paper-over so the test
        // is deterministic.
        var oldGen = fixture.Registry.GetActive("acme")!;
        var reloadResult = await fixture.Registry.ReloadAsync("acme");
        if (reloadResult.Drain is { } drainOp)
            await drainOp.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        var response = await fixture.PostAsync("/admin/acme/force-drain");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _ = oldGen; // pin to keep variable from being optimized; unused otherwise
    }

    [Fact(DisplayName = "Force-drain response includes terminator results")]
    public async Task ForceDrain_WithTerminator_ResponseIncludesTerminatorResults()
    {
        await using var fixture = new ManagementApiFixture(c => c
            .WithAssemblyContaining<ForceDrainEndpointTests>()
            .AddShell("acme", s => s.WithFeature<MgmtStuckDrainWithTerminatorFeature>()));
        await fixture.Registry.GetOrActivateAsync("acme");

        await fixture.PostAsync("/admin/reload/acme");

        var response = await fixture.PostAsync("/admin/acme/force-drain");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DrainResultResponse[]>(WebJson);
        Assert.NotNull(body);
        var terminatorResult = Assert.Single(Assert.Single(body).TerminatorResults);
        Assert.Equal(nameof(MgmtRecordingTerminator), terminatorResult.TerminatorType);
        Assert.Equal("Completed", terminatorResult.Outcome);
        Assert.Null(terminatorResult.ErrorMessage);
    }

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Wraps a <see cref="TestHelpers.StubShellBlueprintProvider"/> so per-test code can
    /// switch <see cref="GetAsync"/> failure on/off after construction (the stub's
    /// <c>ThrowOnGet</c> is shared with <c>ListAsync</c>; this provider isolates them).
    /// </summary>
    private sealed class ProviderWithGetSwitch : IShellBlueprintProvider
    {
        private readonly TestHelpers.StubShellBlueprintProvider _inner = new();

        public Exception? ThrowOnGet { get; set; }

        public void AddBlueprint(string name, Action<CShells.Configuration.ShellBuilder>? configure = null) =>
            _inner.Add(name, configure);

        public Task<ProvidedBlueprint?> GetAsync(string name, CancellationToken cancellationToken = default)
        {
            if (ThrowOnGet is not null)
                throw ThrowOnGet;
            return _inner.GetAsync(name, cancellationToken);
        }

        public Task<BlueprintPage> ListAsync(BlueprintListQuery query, CancellationToken cancellationToken = default)
            => _inner.ListAsync(query, cancellationToken);
    }

    public sealed class MgmtStuckDrainFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services) =>
            services.AddTransient<IDrainHandler, MgmtStuckDrainHandler>();
    }

    private sealed class MgmtStuckDrainHandler : IDrainHandler
    {
        public async Task DrainAsync(IDrainExtensionHandle _, CancellationToken ct)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }
    }

    public sealed class MgmtStuckDrainWithTerminatorFeature : IShellFeature
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IDrainHandler, MgmtStuckDrainHandler>();
            services.AddShellTerminator<MgmtRecordingTerminator>();
        }
    }

    private sealed class MgmtRecordingTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
