using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

public sealed class ApiCorsOptionsValidator : IValidateOptions<ApiCorsOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiCorsOptions options)
    {
        var origins = options.AllowedOrigins.Concat(options.CookieModeOrigins);

        foreach (var origin in origins)
        {
            if (!OriginRules.IsExactOrigin(origin, out _))
            {
                return ValidateOptionsResult.Fail(
                    $"Cors origin '{origin}' must be an exact HTTP(S) origin; wildcards and paths are forbidden.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
