using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

public sealed class AuthSessionOptionsValidator(IOptions<JwtOptions> jwtOptions)
    : IValidateOptions<AuthSessionOptions>
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public ValidateOptionsResult Validate(string? name, AuthSessionOptions options)
    {
        if (_jwt.AccessTokenLifetime >= options.InactivityWindow)
        {
            return ValidateOptionsResult.Fail(
                "Jwt:AccessTokenLifetime must be shorter than Session:InactivityWindow.");
        }

        if (options.InactivityWindow >= options.AbsoluteLifetime)
        {
            return ValidateOptionsResult.Fail(
                "Session:InactivityWindow must be shorter than AbsoluteLifetime.");
        }

        if (options.MfaTicketLifetime > options.InactivityWindow
            || options.WebAuthnChallengeLifetime > options.InactivityWindow)
        {
            return ValidateOptionsResult.Fail(
                "Session challenge lifetimes must not exceed InactivityWindow.");
        }

        return ValidateOptionsResult.Success;
    }
}
