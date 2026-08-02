using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

public sealed class RedisOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<RedisOptions>
{
    public ValidateOptionsResult Validate(string? name, RedisOptions options)
    {
        if (!options.Enabled)
        {
            return options.UseAzureIdentity
                ? ValidateOptionsResult.Fail(
                    "Redis:UseAzureIdentity cannot be enabled while Redis itself is disabled.")
                : ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return ValidateOptionsResult.Fail(
                "Redis:Endpoint is required when Redis is enabled.");
        }

        if (options.Endpoint.Any(char.IsWhiteSpace))
        {
            return ValidateOptionsResult.Fail(
                "Redis:Endpoint cannot contain whitespace.");
        }

        if (environment.IsProductionLike() && !options.UseAzureIdentity)
        {
            return ValidateOptionsResult.Fail(
                "Redis:UseAzureIdentity must be enabled outside Development and Testing; long-lived Redis access keys are not accepted.");
        }

        return ValidateOptionsResult.Success;
    }
}
