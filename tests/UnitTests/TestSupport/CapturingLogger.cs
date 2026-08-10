using Microsoft.Extensions.Logging;

namespace UnitTests.TestSupport;

/// <summary>
/// Records the last event written, for the tests that assert on what a service logged rather
/// than on what it returned.
/// </summary>
/// <remarks>
/// Shared because two suites need it and two copies had already drifted — one captured the
/// exception and the other silently dropped it, so a test could only assert what its own copy
/// happened to keep.
/// </remarks>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public LogLevel? Level { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public Exception? Exception { get; private set; }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Level = logLevel;
        Message = formatter(state, exception);
        Exception = exception;
    }

    private sealed class NoopScope : IDisposable
    {
        public static NoopScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
