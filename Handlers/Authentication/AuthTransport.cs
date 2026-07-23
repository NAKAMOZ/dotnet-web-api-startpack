namespace Api.Handlers.Authentication;

/// <summary>
/// How the current request presented its credential. Set by the authentication handlers,
/// read by the CSRF filter (§14).
/// </summary>
/// <remarks>
/// A marker on <c>HttpContext.Items</c> rather than a claim, because the transport is a
/// property of <em>this request</em> and not of the identity: the same access token is a
/// bearer credential in one call and an ambient cookie in the next, and only the second is
/// reachable by CSRF.
/// <para>
/// Deriving it in the filter instead — "no Authorization header, so it must have been a
/// cookie" — would duplicate the precedence rule in
/// <see cref="ConfigureJwtBearerOptions"/>, and the two would drift the first time that rule
/// changes. The handler that actually read the cookie is the only component that knows.
/// </para>
/// </remarks>
public static class AuthTransport
{
    /// <summary>
    /// Present on <c>HttpContext.Items</c> when the access token was read from the access
    /// <b>cookie</b> rather than from an <c>Authorization</c> header.
    /// </summary>
    public const string CookieAuthenticatedItemKey = "Auth.CookieAuthenticated";
}
