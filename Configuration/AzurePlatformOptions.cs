namespace Api.Configuration;

/// <summary>Azure-hosted production integrations that have no safe implicit fallback.</summary>
public sealed class AzurePlatformOptions
{
    public const string SectionName = "Azure";

    /// <summary>
    /// Versionless Key Vault key identifier used to wrap the persisted Data Protection key
    /// ring, for example https://vault.vault.azure.net/keys/data-protection.
    /// </summary>
    public Uri? DataProtectionKeyIdentifier { get; init; }

    /// <summary>User-assigned managed identity client id; null uses the system identity.</summary>
    public string? ManagedIdentityClientId { get; init; }
}
