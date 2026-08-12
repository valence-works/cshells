using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace CShells.AspNetCore.Middleware;

/// <summary>
/// Root singleton mapping a shell generation to its composed middleware pipeline, built from the
/// shell's <see cref="Features.IMiddlewareShellFeature"/>s when the generation activates and
/// dispatched per request by <see cref="ShellMiddleware"/>.
/// </summary>
/// <remarks>
/// <para>
/// Keys are generation-aware because a reload activates the new generation before the old one
/// deactivates. Entries are removed when a generation reaches Disposed; dispatchers must resolve
/// the pipeline delegate via <see cref="Get"/> <b>before</b> taking a scope on the shell so that
/// a concurrent disposal can never silently skip the pipeline — a resolved delegate stays usable
/// for the request that holds it even after the entry is removed.
/// </para>
/// <para>
/// This registry is root-only infrastructure (excluded from shell containers); the pipeline's
/// terminal rejoins the host pipeline through the <see cref="ShellPipelineContinuation"/> bound
/// on first <see cref="Get"/>.
/// </para>
/// </remarks>
public sealed class ShellMiddlewarePipelineRegistry
{
    private readonly ConcurrentDictionary<(ShellId ShellId, int Generation), Entry> _pipelines = new();

    /// <summary>Registers (or replaces) the pipeline for a shell generation.</summary>
    /// <param name="shellId">The shell identifier.</param>
    /// <param name="generation">The shell generation the pipeline was built for.</param>
    /// <param name="pipeline">The composed pipeline delegate.</param>
    /// <param name="continuation">
    /// The continuation holder the pipeline's terminal rejoins the host pipeline through;
    /// bound to the dispatcher's next-delegate on first <see cref="Get"/>.
    /// </param>
    public void Set(ShellId shellId, int generation, RequestDelegate pipeline, ShellPipelineContinuation continuation)
    {
        Guard.Against.Null(pipeline);
        Guard.Against.Null(continuation);
        _pipelines[(shellId, generation)] = new Entry(pipeline, continuation);
    }

    /// <summary>
    /// Returns the pipeline for a shell generation with its continuation bound to
    /// <paramref name="next"/>, or null if none is registered.
    /// </summary>
    public RequestDelegate? Get(ShellId shellId, int generation, RequestDelegate next)
    {
        Guard.Against.Null(next);
        if (!_pipelines.TryGetValue((shellId, generation), out var entry))
            return null;

        entry.Continuation.Bind(next);
        return entry.Pipeline;
    }

    /// <summary>Removes the pipeline for a shell generation.</summary>
    public void Remove(ShellId shellId, int generation) =>
        _pipelines.TryRemove((shellId, generation), out _);

    private sealed record Entry(RequestDelegate Pipeline, ShellPipelineContinuation Continuation);
}
