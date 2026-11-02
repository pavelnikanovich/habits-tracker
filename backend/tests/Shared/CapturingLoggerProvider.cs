using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace HabitsTracker.Tests;

/// <summary>
/// Records the formatted messages of a logger with their level, category, exception and scopes, for tests of what is
/// logged. The scopes are those the logger factory holds for the line (the request's, and the trace id the factory
/// adds from the current activity), each written as its <c>key:value</c> pairs, or as its text when it has none.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<LogEntry> entries = new();
    private IExternalScopeProvider? scopes;

    public IReadOnlyList<LogEntry> Entries => [.. entries];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;

    public void Dispose()
    {
    }

    public sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception, IReadOnlyList<string> Scopes);

    private List<string> CurrentScopes()
    {
        var current = new List<string>();
        scopes?.ForEachScope((scope, list) => list.Add(Describe(scope)), current);
        return current;
    }

    private static string Describe(object? scope) => scope is IEnumerable<KeyValuePair<string, object?>> pairs
        ? string.Join(", ", pairs.Select(pair => $"{pair.Key}:{pair.Value}"))
        : scope?.ToString() ?? "";

    private sealed class CapturingLogger(string category, CapturingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => provider.scopes?.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            provider.entries.Enqueue(new LogEntry(logLevel, category, formatter(state, exception), exception, provider.CurrentScopes()));
        }
    }
}
