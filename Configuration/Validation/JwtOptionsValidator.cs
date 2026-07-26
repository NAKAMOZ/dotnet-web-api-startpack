using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (!string.Equals(options.Algorithm, "ES256", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail("Jwt:Algorithm must be ES256.");
        }

        if (options.KeyRetirementGrace < options.AccessTokenLifetime + options.ClockSkew)
        {
            return ValidateOptionsResult.Fail(
                "Jwt:KeyRetirementGrace must be at least AccessTokenLifetime + ClockSkew.");
        }

        if (!Uri.TryCreate(options.Issuer, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("Jwt:Issuer must be an absolute URI.");
        }

        return ValidateOptionsResult.Success;
    }
}
