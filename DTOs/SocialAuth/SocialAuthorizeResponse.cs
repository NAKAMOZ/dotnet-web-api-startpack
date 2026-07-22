namespace Api.DTOs.SocialAuth;

/// <summary>
/// The provider authorization URL, for clients that cannot follow the 302 from
/// <c>GET /api/v1/auth/social/{provider}/authorize</c> — an SPA doing a manual redirect,
/// or a native app opening a system browser.
/// </summary>
public sealed record SocialAuthorizeResponse
{
    /// <summary>Fully-formed provider URL, including the signed state parameter.</summary>
    public required string AuthorizationUrl { get; init; }

    /// <summary>
    /// When the embedded state expires. State is signed, short-lived and single-use — it is
    /// what ties the callback to the authorize request that started it, and without that tie
    /// the callback accepts codes obtained by anyone.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
