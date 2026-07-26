using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

public sealed class ApiCorsOptionsValidator : IValidateOptions<ApiCorsOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiCorsOptions options)
    {
        var origins = options.AllowedOrigins.Concat(options.CookieModeOrigins);

        foreach (var origin in origins)
        {
            if (origin == "*"
                || !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || uri.AbsolutePath != "/"
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
            {
                return ValidateOptionsResult.Fail(
                    $"Cors origin '{origin}' must be an exact HTTP(S) origin; wildcards and paths are forbidden.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
