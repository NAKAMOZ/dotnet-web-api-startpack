using Api.DTOs.WellKnown;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Well-known discovery documents. <b>Unversioned</b> — it does not inherit
/// <see cref="ApiControllerBase"/>.
/// </summary>
/// <remarks>
/// <c>/.well-known/jwks.json</c> is fixed by RFC 8615; a verifier looks for exactly that
/// path. Versioning it into <c>/api/v1/…</c> would make it undiscoverable to every standard
/// client, which is the opposite of what a well-known URI is for.
/// </remarks>
[ApiController]
[Produces("application/json")]
[AllowAnonymous]
public sealed class WellKnownController : ControllerBase
{
    /// <summary>The public JSON Web Key Set: the <c>Active</c> and <c>Retiring</c> signing keys.</summary>
    /// <remarks>
    /// Anonymous and cacheable, and safe to publish <em>only while <c>alg</c> stays pinned to
    /// ES256</em>. A validator that read the algorithm from the token header would let an
    /// attacker sign with HS256 using one of these public keys as the HMAC secret
    /// (Authentication.md §2).
    /// <para>
    /// Retired keys are omitted, which is what makes retirement mean anything.
    /// </para>
    /// </remarks>
    [HttpGet("/.well-known/jwks.json")]
    [ProducesResponseType<JwksResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<JwksResponse>> GetJwks(CancellationToken cancellationToken) =>
        Task.FromResult<ActionResult<JwksResponse>>(
            Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Not implemented",
                detail: "The signing-key ring lands in §12."));
}
