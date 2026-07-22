namespace Api.DTOs.Auth;

/// <summary>
/// Response for <c>GET /api/v1/auth/csrf</c>, which also sets the readable
/// <c>__Host-auth.csrf</c> cookie.
/// </summary>
/// <remarks>
/// The token is <b>bound to the session by an HMAC</b>, not merely random
/// (Authentication.md §3). Plain double-submit — compare cookie to header — is defeated by
/// an attacker who can write a cookie for the site, because they can set both halves.
/// Binding means a token minted for another session fails even when the two halves agree.
/// <para>
/// Returning it in the body as well as the cookie is deliberate: a client that cannot read
/// the cookie (a different origin, a strict cookie policy) can still echo the header.
/// </para>
/// </remarks>
public sealed record CsrfTokenResponse
{
    public required string Token { get; init; }

    /// <summary>The header to echo it in — <c>X-CSRF-Token</c>. Named so clients do not hardcode it.</summary>
    public required string HeaderName { get; init; }
}
