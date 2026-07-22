namespace Api.DTOs.Passkeys;

/// <summary>
/// A registered credential, for <c>GET /api/v1/passkeys</c>.
/// </summary>
/// <remarks>
/// Carries no key material — there is no secret to carry. The private key never leaves the
/// authenticator, which is the property that makes passkeys unphishable, and the public key
/// is of no use to a client.
/// </remarks>
public sealed record PasskeyResponse
{
    /// <summary>
    /// Base64url credential id, the value <c>DELETE /api/v1/passkeys/{credentialId}</c>
    /// takes. The delete is scoped to the caller in the same query — fetch-then-compare is
    /// the classic IDOR shape (Authorization.md §5).
    /// </summary>
    public required string CredentialId { get; init; }

    public string? Label { get; init; }

    /// <summary>Authenticator model, when it reports one. Cosmetic — never an authorization input.</summary>
    public Guid? Aaguid { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }
}
