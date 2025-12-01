using System.Collections.Concurrent;
using CShells.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.AspNetCore.Configuration;

/// <summary>
/// Default implementation of <see cref="IShellSettingsCache"/> that loads shell settings at startup
/// and provides synchronous access to the cached data.
/// </summary>
public class DefaultShellSettingsCache : IShellSettingsCache, IHostedService
{
    private readonly IShellSettingsProvider _provider;
    private readonly ILogger<DefaultShellSettingsCache> _logger;
    private readonly ConcurrentDictionary<ShellId, ShellSettings> _cache = new();
    private volatile bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShellSettingsCache"/> class.
    /// </summary>
    /// <param name="provider">The shell settings provider to load settings from.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public DefaultShellSettingsCache(IShellSettingsProvider provider, ILogger<DefaultShellSettingsCache>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
        _logger = logger ?? NullLogger<DefaultShellSettingsCache>.Instance;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ShellSettings> GetAll()
    {
        EnsureInitialized();
        return _cache.Values.ToList();
    }

    /// <inheritdoc />
    public ShellSettings? GetById(ShellId id)
    {
        EnsureInitialized();
        _cache.TryGetValue(id, out var settings);
        return settings;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Loading shell settings into cache...");

        try
        {
            var settings = await _provider.GetShellSettingsAsync(cancellationToken);

            foreach (var shellSettings in settings)
            {
                _cache[shellSettings.Id] = shellSettings;
            }

            _initialized = true;
            _logger.LogInformation("Loaded {Count} shell(s) into cache", _cache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load shell settings into cache");
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cache.Clear();
        _initialized = false;
        return Task.CompletedTask;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "Shell settings cache has not been initialized. Ensure the application host has started.");
        }
    }
}
