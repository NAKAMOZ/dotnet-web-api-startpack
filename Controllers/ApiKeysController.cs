using Api.Attributes;
using Api.DTOs.ApiKeys;
using Api.Models.Enums;
using Api.Services.ApiKeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Personal access tokens for programmatic callers.</summary>
[Route("api/v{version:apiVersion}/api-keys")]
[Authorize]
public sealed class ApiKeysController(IApiKeyService apiKeyService) : ApiControllerBase
{
    /// <summary>Creates a key. The secret is returned here and nowhere else, ever.</summary>
    /// <remarks>
    /// Requested scopes are intersected with the caller's own role-granted permissions at
    /// request time, so a key can never exceed its creator (Authorization.md §7).
    /// </remarks>
    [HttpPost]
    [AuditEvent(AuditEventType.ApiKeyCreated)]
    [ProducesResponseType<CreateApiKeyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CreateApiKeyResponse>> Create(
        [FromBody] CreateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var created = await apiKeyService.CreateAsync(CurrentUserId, request, cancellationToken);
        return Created($"/api/v1/api-keys/{created.Id}", created);
    }

    /// <summary>Lists the caller's keys — prefixes and metadata, never secrets.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ApiKeySummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ApiKeySummaryResponse>>> List(
        CancellationToken cancellationToken) =>
        Ok(await apiKeyService.ListAsync(CurrentUserId, cancellationToken));

    /// <summary>Revokes a key. Scoped to the caller; another user's id answers 404.</summary>
    [HttpDelete("{keyId:guid}")]
    [AuditEvent(AuditEventType.ApiKeyRevoked)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Revoke(Guid keyId, CancellationToken cancellationToken)
    {
        await apiKeyService.RevokeAsync(CurrentUserId, keyId, cancellationToken);
        return NoContent();
    }
}
