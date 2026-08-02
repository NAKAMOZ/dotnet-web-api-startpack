using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

/// <summary>Validates the optional OTLP export boundary.</summary>
public sealed class TelemetryOptionsValidator : IValidateOptions<TelemetryOptions>
{
    public ValidateOptionsResult Validate(string? name, TelemetryOptions options)
    {
        if (options.AzureMonitorExporterEnabled
            && string.IsNullOrWhiteSpace(options.AzureMonitorConnectionString))
        {
            return ValidateOptionsResult.Fail(
                "Telemetry:AzureMonitorConnectionString is required when Azure Monitor export is enabled.");
        }

        if (!options.OtlpExporterEnabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (options.OtlpEndpoint is not { IsAbsoluteUri: true } endpoint
            || endpoint.Scheme is not ("http" or "https"))
        {
            return ValidateOptionsResult.Fail(
                "Telemetry:OtlpEndpoint must be an absolute HTTP(S) URI when OTLP export is enabled.");
        }

        return ValidateOptionsResult.Success;
    }
}
