using Api.DTOs.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>The caller's own sessions — listing and revocation.</summary>
/// <remarks>
/// Every route here resolves the subject from the <c>sub</c> claim. None takes a user id,
/// so there is no cross-user access to authorize and none to get wrong.
/// </remarks>
[Route("api/v{version:apiVersion}/sessions")]
[Authorize]
public sealed class SessionsController : ApiControllerBase
{
    /// <summary>Lists live sessions, flagging the one making this request.</summary>
    /// <remarks>
    /// A security feature rather than a convenience: this list is how a user notices a
    /// session they did not create.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SessionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<IReadOnlyList<SessionResponse>>> List(CancellationToken cancellationToken) =>
        NotImplementedYet<IReadOnlyList<SessionResponse>>();

    /// <summary>Revokes one session.</summary>
    /// <remarks>
    /// The lookup is scoped to the caller in the same query. A session id belonging to
    /// another user answers <c>404</c>, not <c>403</c> — existence is not disclosed
    /// (Authorization.md §11).
    /// </remarks>
    [HttpDelete("{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult> Revoke(Guid sessionId, CancellationToken cancellationToken) =>
        NotImplementedYetResult();

    /// <summary>Revokes every session except the one making the request.</summary>
    [HttpDelete]
    [ProducesResponseType<RevokeSessionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<RevokeSessionsResponse>> RevokeAllOthers(CancellationToken cancellationToken) =>
        NotImplementedYet<RevokeSessionsResponse>();
}
