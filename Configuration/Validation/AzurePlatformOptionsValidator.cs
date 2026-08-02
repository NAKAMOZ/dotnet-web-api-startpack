using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

public sealed class AzurePlatformOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<AzurePlatformOptions>
{
    public ValidateOptionsResult Validate(string? name, AzurePlatformOptions options)
    {
        if (environment.IsProductionLike() && options.DataProtectionKeyIdentifier is null)
        {
            return ValidateOptionsResult.Fail(
                "Azure:DataProtectionKeyIdentifier is required outside Development and Testing so the persisted key ring is encrypted at rest.");
        }

        if (options.DataProtectionKeyIdentifier is { } identifier
            && (!identifier.IsAbsoluteUri
                || identifier.Scheme != Uri.UriSchemeHttps
                || identifier.Query.Length != 0
                || identifier.Fragment.Length != 0
                || identifier.Segments.Length != 3
                || !identifier.Segments[1].Equals("keys/", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(identifier.Segments[2].Trim('/'))))
        {
            return ValidateOptionsResult.Fail(
                "Azure:DataProtectionKeyIdentifier must be a versionless HTTPS Key Vault key URI ending in /keys/{key-name}.");
        }

        if (!string.IsNullOrWhiteSpace(options.ManagedIdentityClientId)
            && !Guid.TryParse(options.ManagedIdentityClientId, out _))
        {
            return ValidateOptionsResult.Fail(
                "Azure:ManagedIdentityClientId must be a GUID when a user-assigned identity is used.");
        }

        return ValidateOptionsResult.Success;
    }
}
