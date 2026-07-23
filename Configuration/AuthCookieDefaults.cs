namespace Api.Configuration;

/// <summary>
/// Default names for the headers cookie mode depends on.
/// </summary>
/// <remarks>
/// Constants rather than literals inside <see cref="AuthCookieOptions"/> because a second
/// component needs the same values at compile time: <see cref="ApiCorsOptions"/> lists them
/// in its default allowed-headers array, and an array initializer cannot read another
/// options instance. Two literals that must agree are two literals that eventually will not
/// — and the failure mode is a browser silently stripping <c>X-CSRF-Token</c> from every
/// cross-origin request, which looks like a broken CSRF filter rather than a typo in CORS.
/// <para>
/// These are defaults only. Both remain overridable through configuration.
/// </para>
/// </remarks>
public static class AuthCookieDefaults
{
    /// <summary>Header a client sets on login to choose the token transport.</summary>
    public const string TransportHeaderName = "X-Auth-Transport";

    /// <summary>Header carrying the echoed CSRF token in cookie mode.</summary>
    public const string CsrfHeaderName = "X-CSRF-Token";
}
