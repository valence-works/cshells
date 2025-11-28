using Microsoft.Extensions.DependencyInjection;

namespace CShells;

/// <summary>
/// Default implementation of <see cref="IShellContextScopeFactory"/> that creates
/// scopes using the shell context's service provider.
/// </summary>
public class DefaultShellContextScopeFactory : IShellContextScopeFactory
{
    /// <inheritdoc/>
    public IShellContextScope CreateScope(ShellContext shellContext)
    {
        ArgumentNullException.ThrowIfNull(shellContext);
        return new DefaultShellContextScope(shellContext);
    }

    private sealed class DefaultShellContextScope : IShellContextScope
    {
        private readonly IServiceScope _serviceScope;
        private bool _disposed;

        public DefaultShellContextScope(ShellContext shellContext)
        {
            ShellContext = shellContext;
            _serviceScope = shellContext.ServiceProvider.CreateScope();
        }

        public ShellContext ShellContext { get; }

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
