using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TodoApi.Tests.Logging;

/// <summary>
/// Captures every log record that reaches it, including structured state.
/// One instance belongs to one factory so parallel tests cannot observe each
/// other's records.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogRecord> _records = new();

    public IReadOnlyList<CapturedLogRecord> Records => _records.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (var (key, value) in pairs)
                {
                    if (key != "{OriginalFormat}")
                    {
                        properties[key] = value;
                    }
                }
            }

            provider._records.Enqueue(new CapturedLogRecord(
                category,
                logLevel,
                eventId,
                formatter(state, exception),
                properties));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

internal sealed record CapturedLogRecord(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Properties);
