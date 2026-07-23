using Serilog.Core;
using Serilog.Events;

namespace UnitTests.Logging;

/// <summary>
/// In-memory sink. Lets a test assert on what a real Serilog pipeline actually emitted,
/// rather than on a hand-built <see cref="LogEvent"/> that never went through the enrichers
/// and the destructuring policy.
/// </summary>
internal sealed class CollectingSink : ILogEventSink
{
    public List<LogEvent> Events { get; } = [];

    public void Emit(LogEvent logEvent) => Events.Add(logEvent);
}
