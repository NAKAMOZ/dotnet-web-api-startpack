using Serilog;
using Serilog.Formatting.Compact;

namespace Api.Logging;

/// <summary>
/// Serilog composition (§15, ADR-0010): the bootstrap logger and the real one.
/// </summary>
/// <remarks>
/// <b>Why two stages.</b> The real logger is built from configuration and from services, so
/// it cannot exist until the container does — which leaves every failure between process
/// start and <c>builder.Build()</c> with nowhere to go. A missing connection string, a
/// malformed options section, an exception inside an <c>Add*</c> extension: all of those
/// throw before any logger exists, and the process dies with whatever the runtime prints to
/// stderr. <see cref="Bootstrap"/> installs a minimal console logger first;
/// <see cref="Configure"/> replaces it once the container can answer.
/// <para>
/// <b>Sinks live here, minimum levels live in configuration.</b> The <c>Serilog</c> section
/// of <c>appsettings.json</c> carries <c>MinimumLevel</c> and its per-source overrides —
/// values an operator changes per environment. It deliberately carries no <c>WriteTo</c>:
/// <c>ReadFrom.Configuration</c> <i>adds</i> sinks rather than replacing them, so a
/// <c>WriteTo</c> in configuration alongside the one below produces two console sinks and
/// every line twice.
/// </para>
/// </remarks>
public static class SerilogSetup
{
    /// <summary>Configuration section the minimum levels are read from.</summary>
    public const string ConfigurationSectionName = "Serilog";

    /// <summary>
    /// Development console format. Ends with the correlation id so a line can be traced back
    /// to a request — the same id the client saw on the response header (§14).
    /// </summary>
    private const string DevelopmentOutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <{CorrelationId}>{NewLine}{Exception}";

    /// <summary>
    /// Installs the bootstrap logger. Called before the host builder exists, so it reads no
    /// configuration and resolves no services.
    /// </summary>
    /// <remarks>
    /// <b><c>CreateLogger</c>, not <c>CreateBootstrapLogger</c>.</b> The bootstrap variant
    /// returns a <i>reloadable</i> logger that the real one later reconfigures in place and
    /// freezes, so that <c>Log.Logger</c> ends up being the configured logger. That upgrade
    /// path assumes one host per process, and this solution violates the assumption in tests:
    /// xUnit runs test classes in parallel, two <c>WebApplicationFactory</c> instances build
    /// two hosts at once, and the second freeze of the one static logger throws
    /// <c>"The logger is already frozen."</c> — a startup crash in a suite that changed
    /// nothing about startup.
    /// <para>
    /// Paired with <c>preserveStaticLogger: true</c> at the registration, this keeps the two
    /// loggers separate: this one owns <c>Log.Logger</c> and covers the window before the
    /// container exists, the configured one is resolved from DI as <c>ILogger&lt;T&gt;</c> and
    /// is what every component in the application actually uses. Nothing in this project logs
    /// through the static <c>Log</c> class after startup, which is what makes the split free.
    /// </para>
    /// </remarks>
    public static void Bootstrap() =>
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

    /// <summary>
    /// Builds the real logger. Invoked by <c>AddSerilog</c> once the container can resolve
    /// the enrichers and the destructuring policy.
    /// </summary>
    public static void Configure(IServiceProvider services, LoggerConfiguration logger)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();

        logger
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", environment.ApplicationName)
            .Enrich.WithProperty("Environment", environment.EnvironmentName)
            .Enrich.WithProperty("MachineName", System.Environment.MachineName)
            .Enrich.With(services.GetRequiredService<CorrelationIdEnricher>())
            .Enrich.With(services.GetRequiredService<UserIdEnricher>())

            // Registered explicitly rather than through ReadFrom.Services. The redaction
            // policy is the one piece here whose absence is silent and unsafe — a token
            // reaching a sink looks exactly like a token not reaching one until somebody
            // reads the logs — so it is wired by a call that fails to compile if it moves,
            // not by a convention that fails to match.
            .Destructure.With(services.GetRequiredService<SensitiveDataDestructuringPolicy>());

        if (environment.IsDevelopment())
        {
            logger.WriteTo.Console(outputTemplate: DevelopmentOutputTemplate);
        }
        else
        {
            // JSON everywhere but Development: the point of structured logging is a log
            // aggregator querying by field, and a human-readable template throws the fields
            // away at the last step.
            logger.WriteTo.Console(new CompactJsonFormatter());
        }
    }
}
