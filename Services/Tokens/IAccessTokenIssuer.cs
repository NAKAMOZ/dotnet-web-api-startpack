namespace Api.Services.Tokens;

/// <summary>
/// Mints ES256-signed access tokens. Implementations assemble the header and payload and
/// delegate the actual signature to <see cref="ISigningKeyManager"/> — private key
/// material never reaches this component (ADR-0020).
/// </summary>
/// <remarks>Implemented in §12. Contract specified in Authentication.md §2.</remarks>
public interface IAccessTokenIssuer
{
    /// <summary>Issues a signed access token with the claim set described in Authentication.md §2.</summary>
    Task<IssuedAccessToken> IssueAsync(AccessTokenRequest request, CancellationToken cancellationToken);
}
