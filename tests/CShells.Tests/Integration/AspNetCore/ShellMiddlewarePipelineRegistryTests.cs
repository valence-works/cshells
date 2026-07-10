using CShells.AspNetCore.Middleware;
using Microsoft.AspNetCore.Http;

namespace CShells.Tests.Integration.AspNetCore;

/// <summary>
/// Tests for <see cref="ShellMiddlewarePipelineRegistry"/> — the generation-aware map from shell
/// to composed middleware pipeline that <see cref="ShellMiddleware"/> dispatches through, and the
/// bind-once <see cref="ShellPipelineContinuation"/> the pipelines rejoin the host pipeline with.
/// </summary>
public class ShellMiddlewarePipelineRegistryTests
{
    private readonly ShellMiddlewarePipelineRegistry _registry = new();
    private static readonly RequestDelegate Pipeline1 = _ => Task.CompletedTask;
    private static readonly RequestDelegate Pipeline2 = _ => Task.CompletedTask;
    private static readonly RequestDelegate Next = _ => Task.CompletedTask;

    private void Set(string name, int generation, RequestDelegate pipeline) =>
        _registry.Set(new ShellId(name), generation, pipeline, new ShellPipelineContinuation());

    [Fact(DisplayName = "Get returns the pipeline registered by Set for the same shell and generation")]
    public void Get_AfterSet_ReturnsPipeline()
    {
        Set("acme", 1, Pipeline1);

        Assert.Same(Pipeline1, _registry.Get(new ShellId("acme"), 1, Next));
    }

    [Fact(DisplayName = "Get returns null for an unregistered shell or generation")]
    public void Get_Unregistered_ReturnsNull()
    {
        Set("acme", 1, Pipeline1);

        Assert.Null(_registry.Get(new ShellId("other"), 1, Next));
        Assert.Null(_registry.Get(new ShellId("acme"), 2, Next));
    }

    [Fact(DisplayName = "Shell names are matched case-insensitively")]
    public void Get_DifferentCasing_ReturnsPipeline()
    {
        Set("Acme", 1, Pipeline1);

        Assert.Same(Pipeline1, _registry.Get(new ShellId("ACME"), 1, Next));
    }

    [Fact(DisplayName = "Set replaces an existing pipeline for the same shell and generation")]
    public void Set_SameKey_ReplacesPipeline()
    {
        Set("acme", 1, Pipeline1);
        Set("acme", 1, Pipeline2);

        Assert.Same(Pipeline2, _registry.Get(new ShellId("acme"), 1, Next));
    }

    [Fact(DisplayName = "Removing one generation leaves other generations intact")]
    public void Remove_OneGeneration_LeavesOthers()
    {
        Set("acme", 1, Pipeline1);
        Set("acme", 2, Pipeline2);

        _registry.Remove(new ShellId("acme"), 1);

        Assert.Null(_registry.Get(new ShellId("acme"), 1, Next));
        Assert.Same(Pipeline2, _registry.Get(new ShellId("acme"), 2, Next));
    }

    [Fact(DisplayName = "Remove of an unknown key is a no-op")]
    public void Remove_UnknownKey_DoesNotThrow()
    {
        _registry.Remove(new ShellId("ghost"), 1);
    }

    [Fact(DisplayName = "Set with a null pipeline or continuation throws ArgumentNullException")]
    public void Set_NullArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _registry.Set(new ShellId("acme"), 1, null!, new ShellPipelineContinuation()));
        Assert.Throws<ArgumentNullException>(() => _registry.Set(new ShellId("acme"), 1, Pipeline1, null!));
    }

    [Fact(DisplayName = "Get binds the continuation to the passed next-delegate; first binding wins")]
    public void Get_BindsContinuation_FirstWins()
    {
        var continuation = new ShellPipelineContinuation();
        _registry.Set(new ShellId("acme"), 1, Pipeline1, continuation);

        _registry.Get(new ShellId("acme"), 1, Next);
        Assert.Same(Next, continuation.Next);

        _registry.Get(new ShellId("acme"), 1, Pipeline2); // different delegate: no rebind
        Assert.Same(Next, continuation.Next);
    }

    [Fact(DisplayName = "An unbound continuation throws instead of silently dropping the request")]
    public void Continuation_Unbound_Throws()
    {
        var continuation = new ShellPipelineContinuation();

        Assert.Throws<InvalidOperationException>(() => continuation.Next);
    }
}
