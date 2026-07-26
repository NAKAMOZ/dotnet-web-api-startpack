using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

public sealed class SocialProviderOptionsValidator : IValidateOptions<SocialProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, SocialProviderOptions options)
    {
        var google = ValidateProvider("Google", options.Google);
        if (google is not null)
        {
            return ValidateOptionsResult.Fail(google);
        }

        var github = ValidateProvider("GitHub", options.GitHub);
        return github is null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(github);
    }

    private static string? ValidateProvider(string name, SocialProviderOptions.Provider provider)
    {
        if (!provider.Enabled)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(provider.ClientId)
               || string.IsNullOrWhiteSpace(provider.ClientSecret)
            ? $"SocialProviders:{name} requires ClientId and ClientSecret when enabled."
            : null;
    }
}
