using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

public sealed class AuthCookieOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<AuthCookieOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthCookieOptions options)
    {
        // Every rule for this options type lives here. Expressing one of them as an inline
        // .Validate lambda at the registration instead would put half the contract somewhere
        // no test of this class can reach.
        if (!options.RequireSecure && environment.IsProductionLike())
        {
            return ValidateOptionsResult.Fail(
                "AuthCookies:RequireSecure must be true outside Development and Testing.");
        }

        if (!options.AccessCookieName.StartsWith("__Host-", StringComparison.Ordinal)
            || !options.CsrfCookieName.StartsWith("__Host-", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "AuthCookies access and CSRF names must use the __Host- prefix.");
        }

        if (!options.RefreshCookieName.StartsWith("__Secure-", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "AuthCookies:RefreshCookieName must use the __Secure- prefix.");
        }

        if (!options.RefreshCookiePath.StartsWith("/", StringComparison.Ordinal)
            || options.RefreshCookiePath == "/")
        {
            return ValidateOptionsResult.Fail(
                "AuthCookies:RefreshCookiePath must be an absolute, endpoint-scoped path.");
        }

        return ValidateOptionsResult.Success;
    }
}
