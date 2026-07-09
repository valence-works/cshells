using Microsoft.AspNetCore.Http;

namespace CShells.AspNetCore.Middleware;

/// <summary>
/// Holder for the host-pipeline continuation a shell's composed middleware pipeline rejoins
/// through. Created per pipeline at composition time; the dispatcher's next-delegate is bound
/// once on the first <see cref="ShellMiddlewarePipelineRegistry.Get"/> (it is constant for the
/// dispatching middleware's lifetime), so dispatching adds no per-request allocation.
/// </summary>
public sealed class ShellPipelineContinuation
{
    private RequestDelegate? _next;

    /// <summary>The bound continuation. Throws if the pipeline is invoked without being obtained through the registry.</summary>
    public RequestDelegate Next => _next ?? throw new InvalidOperationException(
        "Shell middleware pipeline invoked before its continuation was bound. " +
        "Obtain shell pipelines via ShellMiddlewarePipelineRegistry.Get, which binds the continuation.");

    /// <summary>Binds the continuation on first use; later calls are no-ops (first writer wins).</summary>
    internal void Bind(RequestDelegate next) => Interlocked.CompareExchange(ref _next, next, null);
}
