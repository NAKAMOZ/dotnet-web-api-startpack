using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

public sealed class WebAuthnOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<WebAuthnOptions>
{
    public ValidateOptionsResult Validate(string? name, WebAuthnOptions options)
    {
        if (Uri.CheckHostName(options.ServerDomain) is UriHostNameType.Unknown)
        {
            return ValidateOptionsResult.Fail(
                "WebAuthn:ServerDomain must be a DNS host name or IP address without a scheme or port.");
        }

        foreach (var value in options.Origins)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var origin)
                || (origin.Scheme != Uri.UriSchemeHttps && origin.Scheme != Uri.UriSchemeHttp)
                || !string.IsNullOrEmpty(origin.AbsolutePath.Trim('/'))
                || !string.IsNullOrEmpty(origin.Query)
                || !string.IsNullOrEmpty(origin.Fragment)
                || (!string.Equals(origin.Host, options.ServerDomain, StringComparison.OrdinalIgnoreCase)
                    && !origin.Host.EndsWith($".{options.ServerDomain}", StringComparison.OrdinalIgnoreCase)))
            {
                return ValidateOptionsResult.Fail(
                    $"WebAuthn origin '{value}' must be an exact HTTP(S) origin at the RP domain or one of its subdomains.");
            }

            if (origin.Scheme != Uri.UriSchemeHttps && !origin.IsLoopback)
            {
                return ValidateOptionsResult.Fail(
                    $"WebAuthn origin '{value}' must use HTTPS outside loopback development.");
            }

            if (environment.IsProductionLike() && origin.IsLoopback)
            {
                return ValidateOptionsResult.Fail(
                    $"WebAuthn origin '{value}' cannot be loopback outside Development and Testing.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
