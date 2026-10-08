using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CShells.Tests.Integration.Nuplane;

internal sealed class NuplaneTestLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<NuplaneTestLogEntry> _entries = new();

    public IReadOnlyList<NuplaneTestLogEntry> Snapshot() => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public ILogger<T> CreateTypedLogger<T>() => new CapturingLogger<T>(this);

    public void Dispose()
    {
    }

    private void Add(NuplaneTestLogEntry entry) => _entries.Enqueue(entry);

    private sealed class CapturingLogger(NuplaneTestLoggerProvider owner, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            owner.Add(new NuplaneTestLogEntry(categoryName, logLevel, eventId, exception, properties, formatter(state, exception)));
        }
    }

    private sealed class CapturingLogger<T>(NuplaneTestLoggerProvider owner) : ILogger<T>
    {
        private readonly CapturingLogger innerLogger = new(owner, typeof(T).FullName ?? typeof(T).Name);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => innerLogger.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => innerLogger.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            innerLogger.Log(logLevel, eventId, state, exception, formatter);
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

internal sealed record NuplaneTestLogEntry(
    string Category,
    LogLevel Level,
    EventId EventId,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties,
    string Message);
