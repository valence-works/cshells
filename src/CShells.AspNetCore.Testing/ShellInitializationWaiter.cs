namespace CShells.AspNetCore.Testing;

/// <summary>
/// Provides a mechanism to wait for shell initialization to complete during application startup.
/// Useful for testing scenarios where you need to ensure shells and endpoints are fully registered
/// before making requests.
/// </summary>
public class ShellInitializationWaiter
{
    private readonly TaskCompletionSource<bool> _initializationComplete = new();
    private readonly object _lock = new();
    private bool _isCompleted;

    /// <summary>
    /// Gets a task that completes when shell initialization is finished.
    /// </summary>
    public Task InitializationTask => _initializationComplete.Task;

    /// <summary>
    /// Gets a value indicating whether initialization has completed.
    /// </summary>
    public bool IsCompleted
    {
        get
        {
            lock (_lock)
            {
                return _isCompleted;
            }
        }
    }

    /// <summary>
    /// Signals that shell initialization has completed successfully.
    /// </summary>
    public void SignalComplete()
    {
        lock (_lock)
        {
            if (!_isCompleted)
            {
                _isCompleted = true;
                _initializationComplete.TrySetResult(true);
            }
        }
    }

    /// <summary>
    /// Waits for shell initialization to complete with an optional timeout.
    /// </summary>
    /// <param name="timeout">The maximum time to wait. If null, waits indefinitely.</param>
    /// <returns>True if initialization completed within the timeout; otherwise false.</returns>
    public async Task<bool> WaitForInitializationAsync(TimeSpan? timeout = null)
    {
        if (IsCompleted)
            return true;

        if (timeout.HasValue)
        {
            var completedTask = await Task.WhenAny(_initializationComplete.Task, Task.Delay(timeout.Value));
            return completedTask == _initializationComplete.Task;
        }

        await _initializationComplete.Task;
        return true;
    }
}
