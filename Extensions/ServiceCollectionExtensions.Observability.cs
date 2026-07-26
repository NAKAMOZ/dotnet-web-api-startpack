using Api.Logging;
using Serilog;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Serilog, OpenTelemetry traces and metrics, health checks, and caching.
    /// </summary>
    public static IServiceCollection AddObservabilityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Both enrichers read the ambient request. Nothing else in the project needs the
        // accessor — controllers read claims, services take what they need as arguments —
        // so this registration exists for logging alone (ADR-0010).
        services.AddHttpContextAccessor();

        // Singletons, resolved once when the logger is built. They hold no request state:
        // the accessor is what varies per request, and it is itself a singleton over an
        // AsyncLocal.
        services.AddSingleton<CorrelationIdEnricher>();
        services.AddSingleton<UserIdEnricher>();
        services.AddSingleton<SensitiveDataDestructuringPolicy>();

        // Replaces the built-in logging providers rather than adding to them
        // (writeToProviders stays false): two logging systems means every event formatted
        // twice, and the redaction policy applies to only one of them.
        //
        // preserveStaticLogger: true leaves Program.cs's bootstrap logger owning the static
        // Log.Logger instead of reconfiguring and freezing it. The freeze is per-process and
        // this process may build more than one host — see the remarks on SerilogSetup.Bootstrap.
        services.AddSerilog(SerilogSetup.Configure, preserveStaticLogger: true);

        // TODO §12: HybridCache, in-memory only (ADR-0016).
        return services
            .AddApplicationHealthChecks(configuration)
            .AddTelemetryServices(configuration);
    }
}
