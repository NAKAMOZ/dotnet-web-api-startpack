using System.Reflection;
using Api.Configuration;
using Api.Logging;
using Api.Services.Monitoring;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers backend-neutral OpenTelemetry traces and metrics with optional OTLP export.
    /// </summary>
    public static IServiceCollection AddTelemetryServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Read at registration time, because whether the exporter is registered at all is a
        // registration-time decision. AddValidatedOptions binds and validates the same
        // section for everything that resolves it later; a bad endpoint here still fails the
        // host at ValidateOnStart rather than exporting to nowhere.
        var configured = configuration
            .GetSection(TelemetryOptions.SectionName)
            .Get<TelemetryOptions>() ?? new TelemetryOptions();
        var otlpEndpoint = configured is { OtlpExporterEnabled: true, OtlpEndpoint: { } endpoint }
            ? endpoint
            : null;
        var version = typeof(ServiceCollectionExtensions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        services.AddSingleton<AuthMetrics>();
        services.AddHostedService<ActiveSessionMetricsCollector>();

        var telemetry = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    configured.ServiceName,
                    serviceVersion: version,
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(
                [
                    new("deployment.environment.name", environment.EnvironmentName),
                ]));

        telemetry.WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql();

            if (otlpEndpoint is not null)
            {
                tracing.AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint);
            }
        });

        telemetry.WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(AuthMetrics.MeterName)
                .AddMeter("Npgsql");

            if (otlpEndpoint is not null)
            {
                metrics.AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint);
            }
        });

        return services;
    }
}
