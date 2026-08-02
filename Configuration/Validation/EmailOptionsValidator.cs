using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

/// <summary>Prevents deployed SMTP from silently retaining local or partial credentials.</summary>
public sealed class EmailOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        var hasUsername = !string.IsNullOrWhiteSpace(options.Username);
        var hasPassword = !string.IsNullOrWhiteSpace(options.Password);

        if (hasUsername != hasPassword)
        {
            return ValidateOptionsResult.Fail(
                "Email:Username and Email:Password must either both be configured or both be absent.");
        }

        if (environment.IsProductionLike()
            && (!options.UseTls
                || string.Equals(options.Host, "localhost", StringComparison.OrdinalIgnoreCase)))
        {
            return ValidateOptionsResult.Fail(
                "Email must use TLS and a non-localhost SMTP host outside Development and Testing.");
        }

        return ValidateOptionsResult.Success;
    }
}
