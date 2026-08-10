using System.Diagnostics.CodeAnalysis;

namespace Api.Configuration.Validation;

internal static class OriginRules
{
    /// <summary>
    /// Whether a configured value is an exact HTTP(S) origin — scheme, host and optional port,
    /// and nothing else.
    /// </summary>
    /// <remarks>
    /// Written once because CORS and WebAuthn both ask it of operator-supplied lists, and two
    /// spellings would mean a hardening change (rejecting userinfo, say) landing on one of
    /// them. Callers keep only the rules that are actually theirs: CORS has none beyond this,
    /// WebAuthn adds the relying-party domain and loopback rules.
    /// <para>
    /// A wildcard needs no special case: <c>*</c> is not an absolute URI, so it fails here.
    /// </para>
    /// </remarks>
    public static bool IsExactOrigin(string value, [NotNullWhen(true)] out Uri? origin) =>
        Uri.TryCreate(value, UriKind.Absolute, out origin)
        && (origin.Scheme == Uri.UriSchemeHttp || origin.Scheme == Uri.UriSchemeHttps)
        && origin.AbsolutePath == "/"
        && string.IsNullOrEmpty(origin.Query)
        && string.IsNullOrEmpty(origin.Fragment)
        && string.IsNullOrEmpty(origin.UserInfo);
}
