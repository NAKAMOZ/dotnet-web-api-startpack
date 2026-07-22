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
        // TODO §15: Serilog with correlation-ID and user-ID enrichers (ADR-0010).
        // TODO §28: OpenTelemetry traces and metrics; /health/live and /health/ready.
        // TODO §12: HybridCache, in-memory only (ADR-0016).
        return services;
    }
}
