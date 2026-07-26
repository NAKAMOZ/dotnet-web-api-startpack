using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

public sealed class AuthCookieOptionsValidator : IValidateOptions<AuthCookieOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthCookieOptions options)
    {
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
