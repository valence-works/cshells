using Microsoft.Extensions.DependencyInjection;

namespace CShells.Hosting;

/// <summary>
/// Default implementation of <see cref="IShellContextScopeFactory"/> that creates
/// scopes using the shell context's service provider.
/// </summary>
public class DefaultShellContextScopeFactory : IShellContextScopeFactory
{
    /// <inheritdoc/>
    public IShellContextScope CreateScope(ShellContext shellContext)
    {
        return new DefaultShellContextScope(Guard.Against.Null(shellContext));
    }

    private sealed class DefaultShellContextScope(ShellContext shellContext) : IShellContextScope
    {
        private readonly IServiceScope _serviceScope = shellContext.ServiceProvider.CreateScope();
        private bool _disposed;

        public ShellContext ShellContext { get; } = shellContext;

        public IServiceProvider ServiceProvider => _serviceScope.ServiceProvider;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _serviceScope.Dispose();
        }
    }
}
