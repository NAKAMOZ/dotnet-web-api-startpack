using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Api.Handlers.Authentication;

/// <summary>
/// An authentication scheme that authenticates nobody. <b>Temporary — §12 replaces it.</b>
/// </summary>
/// <remarks>
/// <para>
/// It exists to make the pipeline coherent while the real schemes are still unwritten.
/// ASP.NET Core cannot issue a 401 without a challenge scheme: an <c>[Authorize]</c>
/// endpoint with none registered throws <c>InvalidOperationException</c>, so every
/// protected route in §11 would answer <b>500</b> instead of 401. That would make the
/// controllers untestable and, worse, would make an authorization failure look like a
/// server fault.
/// </para>
/// <para>
/// It fails closed by construction — <see cref="HandleAuthenticateAsync"/> returns
/// <see cref="AuthenticateResult.NoResult"/> unconditionally, so no request can ever be
/// authenticated through it. If this is still registered when §12 lands, the symptom is
/// that <b>every</b> protected endpoint returns 401 and nobody can log in. Loud, and safe.
/// </para>
/// </remarks>
public sealed class PlaceholderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Scheme name. Referenced only by the registration in §12's extension.</summary>
    public const string SchemeName = "Placeholder";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}
