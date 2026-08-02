using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

public sealed class JwtOptionsValidator(IHostEnvironment environment) : IValidateOptions<JwtOptions>
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

        if (!Uri.TryCreate(options.Issuer, UriKind.Absolute, out var issuer))
        {
            return ValidateOptionsResult.Fail("Jwt:Issuer must be an absolute URI.");
        }

        if (environment.IsProductionLike()
            && (issuer.Scheme != Uri.UriSchemeHttps || issuer.IsLoopback))
        {
            return ValidateOptionsResult.Fail(
                "Jwt:Issuer must be a non-loopback HTTPS URI outside Development and Testing.");
        }

        return ValidateOptionsResult.Success;
    }
}
