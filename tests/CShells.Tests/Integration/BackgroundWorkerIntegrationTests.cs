using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CShells.SampleApp.Background;

namespace CShells.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ShellDemoWorker"/> verifying it properly notifies observers.
/// </summary>
public class BackgroundWorkerIntegrationTests : IDisposable
{
    private readonly List<CShells.DefaultShellHost> _hostsToDispose = [];

    public void Dispose()
    {
        foreach (var host in _hostsToDispose)
        {
            host.Dispose();
        }
    }

    [Fact(DisplayName = "ShellDemoWorker notifies observer for each shell")]
    public async Task ShellDemoWorker_NotifiesObserver_ForEachShell()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new ShellId("Shell1")),
            new ShellSettings(new ShellId("Shell2"))
        };
        var shellHost = new CShells.DefaultShellHost(settings, []);
        _hostsToDispose.Add(shellHost);

        var scopeFactory = new DefaultShellContextScopeFactory();
        var observer = new TestBackgroundWorkObserver();
        var logger = new TestLogger<ShellDemoWorker>();

        var worker = new ShellDemoWorker(shellHost, scopeFactory, observer, logger);

        using var cts = new CancellationTokenSource();

        // Act - Start the worker and let it run briefly, then cancel
        var workerTask = worker.StartAsync(cts.Token);

        // Wait a short time to let the worker execute one iteration
        await Task.Delay(100);
        cts.Cancel();

        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }

        // Assert
        Assert.Equal(2, observer.WorkExecutions.Count);
        Assert.Contains(observer.WorkExecutions, e => e.ShellId.Name == "Shell1");
        Assert.Contains(observer.WorkExecutions, e => e.ShellId.Name == "Shell2");
    }

    [Fact(DisplayName = "ShellDemoWorker constructor throws on null shellHost")]
    public void ShellDemoWorker_Constructor_ThrowsOnNullShellHost()
    {
        // Arrange
        var scopeFactory = new DefaultShellContextScopeFactory();
        var logger = new TestLogger<ShellDemoWorker>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ShellDemoWorker(null!, scopeFactory, null, logger));
        Assert.Equal("shellHost", ex.ParamName);
    }

    [Fact(DisplayName = "ShellDemoWorker constructor throws on null scopeFactory")]
    public void ShellDemoWorker_Constructor_ThrowsOnNullScopeFactory()
    {
        // Arrange
        var settings = new[] { new ShellSettings(new ShellId("Shell1")) };
        var shellHost = new CShells.DefaultShellHost(settings, []);
        _hostsToDispose.Add(shellHost);
        var logger = new TestLogger<ShellDemoWorker>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ShellDemoWorker(shellHost, null!, null, logger));
        Assert.Equal("scopeFactory", ex.ParamName);
    }

    [Fact(DisplayName = "ShellDemoWorker constructor throws on null logger")]
    public void ShellDemoWorker_Constructor_ThrowsOnNullLogger()
    {
        // Arrange
        var settings = new[] { new ShellSettings(new ShellId("Shell1")) };
        var shellHost = new CShells.DefaultShellHost(settings, []);
        _hostsToDispose.Add(shellHost);
        var scopeFactory = new DefaultShellContextScopeFactory();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ShellDemoWorker(shellHost, scopeFactory, null, null!));
        Assert.Equal("logger", ex.ParamName);
    }

    [Fact(DisplayName = "ShellDemoWorker works without observer")]
    public async Task ShellDemoWorker_WorksWithoutObserver()
    {
        // Arrange
        var settings = new[]
        {
            new ShellSettings(new ShellId("Shell1"))
        };
        var shellHost = new CShells.DefaultShellHost(settings, []);
        _hostsToDispose.Add(shellHost);

        var scopeFactory = new DefaultShellContextScopeFactory();
        var logger = new TestLogger<ShellDemoWorker>();

        var worker = new ShellDemoWorker(shellHost, scopeFactory, null, logger);

        using var cts = new CancellationTokenSource();

        // Act - Start the worker and let it run briefly, then cancel
        var workerTask = worker.StartAsync(cts.Token);

        // Wait a short time to let the worker execute
        await Task.Delay(100);
        cts.Cancel();

        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }

        // Assert - Should complete without throwing
        Assert.True(logger.LoggedMessages.Count > 0);
    }

    /// <summary>
    /// Test observer that records all work executions.
    /// </summary>
    private class TestBackgroundWorkObserver : IBackgroundWorkObserver
    {
        public List<(ShellId ShellId, string WorkDescription)> WorkExecutions { get; } = [];

        public void OnWorkExecuted(ShellId shellId, string workDescription)
        {
            WorkExecutions.Add((shellId, workDescription));
        }
    }

    /// <summary>
    /// Simple test logger that records log messages.
    /// </summary>
    private class TestLogger<T> : ILogger<T>
    {
        public List<string> LoggedMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LoggedMessages.Add(formatter(state, exception));
        }
    }
}
