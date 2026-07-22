using Api.DTOs.ApiKeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Personal access tokens for programmatic callers.</summary>
[Route("api/v{version:apiVersion}/api-keys")]
[Authorize]
public sealed class ApiKeysController : ApiControllerBase
{
    /// <summary>Creates a key. The secret is returned here and nowhere else, ever.</summary>
    /// <remarks>
    /// Requested scopes are intersected with the caller's own role-granted permissions at
    /// request time, so a key can never exceed its creator (Authorization.md §7).
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<CreateApiKeyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<ActionResult<CreateApiKeyResponse>> Create(
        [FromBody] CreateApiKeyRequest request,
        CancellationToken cancellationToken) =>
        NotImplementedYet<CreateApiKeyResponse>();

    /// <summary>Lists the caller's keys — prefixes and metadata, never secrets.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ApiKeySummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<IReadOnlyList<ApiKeySummaryResponse>>> List(CancellationToken cancellationToken) =>
        NotImplementedYet<IReadOnlyList<ApiKeySummaryResponse>>();

    /// <summary>Revokes a key. Scoped to the caller; another user's id answers 404.</summary>
    [HttpDelete("{keyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult> Revoke(Guid keyId, CancellationToken cancellationToken) =>
        NotImplementedYetResult();
}
