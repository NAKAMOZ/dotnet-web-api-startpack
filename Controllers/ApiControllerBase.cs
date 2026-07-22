using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Shared base for every versioned controller. Routing conventions and claim accessors —
/// nothing else.
/// </summary>
/// <remarks>
/// Controllers in this project are <b>thin by rule</b>: an action maps the request, makes
/// one service call, and maps the result to a status. Anything that branches beyond status
/// selection belongs in a service, where it can be unit-tested without an HTTP context.
/// <para>
/// Controllers never read tokens, cookies or headers. Authentication handlers turn those
/// into claims; controllers read claims. That separation is what keeps "who is calling"
/// answerable in one place instead of thirteen.
/// </para>
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// The caller's user id, from the <c>sub</c> claim.
    /// </summary>
    /// <remarks>
    /// Self-service routes resolve the subject from here and <b>never</b> from a route
    /// parameter — which is why they have no IDOR surface to get wrong
    /// (Authorization.md §5).
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The claim is absent or unparseable. Reaching this on an authenticated endpoint means
    /// the token was issued without a subject, which is a bug rather than a client error —
    /// hence a throw and a 500, not a 401.
    /// </exception>
    protected Guid CurrentUserId =>
        ClaimAsGuid(ClaimTypes.NameIdentifier, "sub");

    /// <summary>
    /// The caller's session id, from the <c>sid</c> claim.
    /// </summary>
    /// <remarks>
    /// Ties the request to one session row — what "revoke all except current" and
    /// <c>IsCurrent</c> in the session list are computed against.
    /// </remarks>
    protected Guid CurrentSessionId => ClaimAsGuid("sid");

    /// <summary>
    /// Placeholder result for actions whose service does not exist yet (§12).
    /// </summary>
    /// <remarks>
    /// A routed, annotated, validated endpoint that answers <c>501</c> is honest: the route
    /// exists and its contract is fixed, the behaviour is not written. Returning a fabricated
    /// success would be worse than useless — it would make the endpoint look finished to
    /// every client and every test.
    /// </remarks>
    protected ObjectResult NotImplementedYet() =>
        Problem(
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented",
            detail: "This endpoint is routed, annotated and validated. Its service implementation lands in §12.");

    /// <summary>Typed convenience wrapper over <see cref="NotImplementedYet"/>.</summary>
    protected Task<ActionResult<T>> NotImplementedYet<T>() =>
        Task.FromResult<ActionResult<T>>(NotImplementedYet());

    /// <summary>Untyped convenience wrapper over <see cref="NotImplementedYet"/>.</summary>
    protected Task<ActionResult> NotImplementedYetResult() =>
        Task.FromResult<ActionResult>(NotImplementedYet());

    private Guid ClaimAsGuid(params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            if (Guid.TryParse(User.FindFirstValue(claimType), out var value))
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"No parseable claim among [{string.Join(", ", claimTypes)}] on an authenticated request.");
    }
}
