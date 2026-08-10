using System.Collections.Concurrent;
using Api.Logging;
using Serilog.Events;

namespace IntegrationTests.Infrastructure;

public sealed class CollectingLogSink : IDiagnosticLogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyList<LogEvent> Snapshot() => [.. _events];

    public void Clear()
    {
        while (_events.TryDequeue(out _))
        {
        }
    }

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
}
