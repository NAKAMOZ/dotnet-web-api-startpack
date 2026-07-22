namespace Api.DTOs.Admin;

/// <summary>Body for <c>POST /api/v1/admin/users/{userId}/roles</c>.</summary>
/// <remarks>
/// Takes the role id rather than the name. The name is what lands in a token and what the
/// permission map is keyed by, so accepting it here would let a typo create a grant that
/// silently matches nothing — an id either resolves to a row or 404s.
/// </remarks>
public sealed record AssignRoleRequest
{
    public required Guid RoleId { get; init; }
}
