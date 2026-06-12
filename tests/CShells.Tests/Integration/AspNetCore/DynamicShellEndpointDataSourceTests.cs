using CShells.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace CShells.Tests.Integration.AspNetCore;

public class DynamicShellEndpointDataSourceTests
{
    [Fact(DisplayName = "RemoveEndpoints by generation preserves endpoints from other generations")]
    public void RemoveByGeneration_PreservesOtherGenerations()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();

        var gen1Endpoint = CreateEndpoint("default/api/hello", shellId, generation: 1, settings);
        var gen2Endpoint = CreateEndpoint("default/api/hello", shellId, generation: 2, settings);

        dataSource.AddEndpoints([gen1Endpoint]);
        dataSource.AddEndpoints([gen2Endpoint]);
        Assert.Equal(2, dataSource.Endpoints.Count);

        // Remove only generation 1 — simulates old shell deactivating after reload.
        dataSource.RemoveEndpoints(shellId, generation: 1);

        Assert.Single(dataSource.Endpoints);
        var remaining = (RouteEndpoint)dataSource.Endpoints[0];
        Assert.Equal(2, remaining.Metadata.GetMetadata<ShellEndpointMetadata>()!.Generation);
    }

    [Fact(DisplayName = "RemoveEndpoints by ShellId removes all generations")]
    public void RemoveByShellId_RemovesAllGenerations()
    {
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();

        dataSource.AddEndpoints([CreateEndpoint("default/api/a", shellId, 1, settings)]);
        dataSource.AddEndpoints([CreateEndpoint("default/api/a", shellId, 2, settings)]);
        Assert.Equal(2, dataSource.Endpoints.Count);

        dataSource.RemoveEndpoints(shellId);

        Assert.Empty(dataSource.Endpoints);
    }

    [Fact(DisplayName = "Reload sequence: new generation endpoints survive old generation teardown")]
    public void ReloadSequence_NewEndpointsSurviveOldTeardown()
    {
        // Simulates the exact reload sequence from ShellRegistry.ReloadAsync:
        // 1. New shell (gen 2) transitions Initializing→Active → register gen 2 endpoints
        // 2. Old shell (gen 1) transitions Active→Deactivating → remove gen 1 endpoints only
        var dataSource = new DynamicShellEndpointDataSource();
        var shellId = new ShellId("default");
        var settings = new ShellSettings();

        // Step 0: Initial activation — gen 1 endpoints registered.
        dataSource.AddEndpoints([CreateEndpoint("default/api/items", shellId, 1, settings)]);

        // Step 1: Reload creates gen 2, which removes-then-adds (as the handler does).
        dataSource.RemoveEndpoints(shellId);
        dataSource.AddEndpoints([CreateEndpoint("default/api/items", shellId, 2, settings)]);

        // Step 2: Old gen 1 deactivates — only removes its own generation.
        dataSource.RemoveEndpoints(shellId, generation: 1);

        // Gen 2 endpoint must survive.
        Assert.Single(dataSource.Endpoints);
        var survivor = (RouteEndpoint)dataSource.Endpoints[0];
        Assert.Equal(2, survivor.Metadata.GetMetadata<ShellEndpointMetadata>()!.Generation);
    }

    private static RouteEndpoint CreateEndpoint(string pattern, ShellId shellId, int generation, ShellSettings settings)
    {
        return new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(pattern),
            order: 0,
            new EndpointMetadataCollection(
                new ShellEndpointMetadata(shellId, generation, settings),
                new HttpMethodMetadata(["GET"])),
            displayName: $"{pattern} (gen {generation})");
    }
}
