using Serilog.Core;

namespace Api.Logging;

/// <summary>
/// A sink a host deliberately attaches for verification, on top of the sinks
/// <see cref="SerilogSetup.Configure"/> owns.
/// </summary>
/// <remarks>
/// Deliberately narrower than <see cref="ILogEventSink"/>. Discovering plain
/// <c>ILogEventSink</c> registrations would make log fan-out depend on whatever happens to
/// land in the container — several Serilog integration packages register one — so a package
/// reference alone could add a live production destination with no configuration surface and
/// no way to switch it off. Requiring this marker keeps every destination declared.
/// <para>
/// Implementations still receive events only after the enrichment and redaction policy in
/// <see cref="SerilogSetup.Configure"/>; the guarantee they must not bypass is that one.
/// </para>
/// </remarks>
public interface IDiagnosticLogEventSink : ILogEventSink;
