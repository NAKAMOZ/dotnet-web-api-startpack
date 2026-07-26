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
        IConfiguration configuration)
    {
        var configured = configuration
            .GetSection(TelemetryOptions.SectionName)
            .Get<TelemetryOptions>() ?? new TelemetryOptions();
        var assembly = typeof(ServiceCollectionExtensions).Assembly;
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        services.AddSingleton<AuthMetrics>();
        services.AddHostedService<ActiveSessionMetricsInitializer>();

        var telemetry = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    configured.ServiceName,
                    serviceVersion: version,
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(
                [
                    new("deployment.environment.name",
                        configuration[HostDefaults.EnvironmentKey]
                        ?? configuration["ASPNETCORE_ENVIRONMENT"]
                        ?? Environments.Production),
                ]));

        telemetry.WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql();

            if (configured is { OtlpExporterEnabled: true, OtlpEndpoint: not null })
            {
                tracing.AddOtlpExporter(exporter => exporter.Endpoint = configured.OtlpEndpoint);
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

            if (configured is { OtlpExporterEnabled: true, OtlpEndpoint: not null })
            {
                metrics.AddOtlpExporter(exporter => exporter.Endpoint = configured.OtlpEndpoint);
            }
        });

        return services;
    }
}
