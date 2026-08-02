using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

/// <summary>Backend-neutral OpenTelemetry configuration (§28).</summary>
public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>Stable service name attached to every trace and metric resource.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100, MinimumLength = 1)]
    public string ServiceName { get; init; } = "dotnet-web-api-startpack";

    /// <summary>
    /// Whether traces and metrics are exported over OTLP. Local instrumentation remains
    /// registered when this is false, so <c>MeterListener</c> and diagnostic tools work.
    /// </summary>
    public bool OtlpExporterEnabled { get; init; }

    /// <summary>OTLP collector endpoint. Required when export is enabled.</summary>
    public Uri? OtlpEndpoint { get; init; }

    /// <summary>Exports traces and metrics to Azure Monitor Application Insights.</summary>
    public bool AzureMonitorExporterEnabled { get; init; }

    /// <summary>Application Insights connection string. Treat as a deployment secret.</summary>
    public string? AzureMonitorConnectionString { get; init; }
}
