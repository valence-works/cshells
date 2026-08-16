using CShells.Lifecycle;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;

namespace CShells.AspNetCore.Routing;

/// <summary>
/// Acquires the exact shell-generation scope while endpoint matching still owns the selected
/// generation. This closes the handoff gap between routing and shell middleware during reload.
/// </summary>
internal sealed class ShellEndpointGenerationMatcherPolicy(IShellRegistry registry) :
    MatcherPolicy,
    IEndpointSelectorPolicy
{
    private readonly IShellRegistry registry = Guard.Against.Null(registry);

    // Run after framework method, host, and constraint policies have invalidated candidates.
    public override int Order => int.MaxValue;

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints) =>
        endpoints.Any(endpoint => endpoint.Metadata.GetMetadata<ShellEndpointMetadata>() is not null);

    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        Guard.Against.Null(httpContext);
        Guard.Against.Null(candidates);

        // Matcher policy execution may be re-entered. One request owns at most one generation
        // lease, registered for release at response completion when it is first acquired.
        if (httpContext.Features.Get<ShellEndpointGenerationLease>() is not null)
            return Task.CompletedTask;

        for (var i = 0; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i))
                continue;

            var metadata = candidates[i].Endpoint.Metadata.GetMetadata<ShellEndpointMetadata>();
            if (metadata is null)
                continue;

            var shell = registry.GetAll(metadata.ShellId.Name)
                .FirstOrDefault(candidate => candidate.Descriptor.Generation == metadata.Generation);
            if (shell is null)
            {
                candidates.SetValidity(i, false);
                continue;
            }

            if (ShellEndpointGenerationLease.TryAcquire(httpContext, metadata, shell, out _))
            {
                return Task.CompletedTask;
            }

            // Drain disposal won the race before this candidate became a successful match.
            candidates.SetValidity(i, false);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Request-local exact-generation scope acquired by endpoint matching.</summary>
internal sealed class ShellEndpointGenerationLease(
    ShellId shellId,
    int generation,
    IShellScope scope) : IAsyncDisposable
{
    private readonly IShellScope scope = Guard.Against.Null(scope);
    private int disposed;

    internal ShellId ShellId { get; } = shellId;
    internal int Generation { get; } = generation;
    internal IShellScope Scope => scope;

    internal bool Matches(ShellEndpointMetadata metadata) =>
        ShellId.Equals(metadata.ShellId) && Generation == metadata.Generation;

    internal static bool TryAcquire(
        HttpContext context,
        ShellEndpointMetadata metadata,
        IShell shell,
        out ShellEndpointGenerationLease? lease)
    {
        var existing = context.Features.Get<ShellEndpointGenerationLease>();
        if (existing is not null)
        {
            lease = existing.Matches(metadata) ? existing : null;
            return lease is not null;
        }

        if (!string.Equals(shell.Descriptor.Name, metadata.ShellId.Name, StringComparison.OrdinalIgnoreCase)
            || shell.Descriptor.Generation != metadata.Generation)
        {
            lease = null;
            return false;
        }

        var scope = TryBeginScope(shell);
        if (scope is null)
        {
            lease = null;
            return false;
        }

        var acquired = new ShellEndpointGenerationLease(metadata.ShellId, metadata.Generation, scope);
        try
        {
            context.Response.OnCompleted(
                static state => ((ShellEndpointGenerationLease)state).DisposeAsync().AsTask(),
                acquired);
            context.Features.Set(acquired);
            lease = acquired;
            return true;
        }
        catch
        {
            acquired.DisposeAsync().AsTask().GetAwaiter().GetResult();
            lease = null;
            throw;
        }
    }

    private static IShellScope? TryBeginScope(IShell shell)
    {
        try
        {
            return shell.BeginScope();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        await scope.DisposeAsync().ConfigureAwait(false);
    }
}
