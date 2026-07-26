---
method: DELETE
route: /api/v1/admin/users/{userId}/roles/{roleId}
auth: required
---

# Revoke role

## Purpose
Removes one role assignment from a user.
## HTTP method
`DELETE`
## Route
`/api/v1/admin/users/{userId}/roles/{roleId}`
## Authentication requirements
Bearer JWT, access cookie, or API key.
## Authorization requirements
Permission `roles:revoke`.
## Request headers
Cookie mode requires a valid CSRF header.
## Route parameters
`userId`: target user GUID; `roleId`: role GUID.
## Query parameters
None.
## Request body
None.
## Validation rules
The assignment must exist and any protected-role invariants must remain satisfied.
## Success response
`204 No Content`.
## Error responses
`401 unauthorized`; `403 forbidden` or `csrf_validation_failed`; `404 not_found`; `429 rate_limited`.
## Example request
```http
DELETE /api/v1/admin/users/01900000-0000-7000-8000-000000000005/roles/01900000-0000-7000-8000-000000000006
Authorization: Bearer <admin-token>
```
## Example response
```http
HTTP/1.1 204 No Content
```
## Security considerations
Revocation is audit-recorded. Existing access tokens may carry the old role for at most the access-token lifetime.
## Related endpoints
[`Grant role`](GrantRole.md), [`Revoke user sessions`](RevokeUserSessions.md).
