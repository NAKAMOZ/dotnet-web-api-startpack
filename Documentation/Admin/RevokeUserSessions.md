---
method: DELETE
route: /api/v1/admin/users/{userId}/sessions
auth: required
---

# Admin revoke user sessions

## Purpose
Revokes every refresh-capable session for one user.
## HTTP method
`DELETE`
## Route
`/api/v1/admin/users/{userId}/sessions`
## Authentication requirements
Bearer JWT, access cookie, or API key.
## Authorization requirements
Permission `sessions:revoke:any`.
## Request headers
Cookie mode requires a valid CSRF header.
## Route parameters
`userId`: target user GUID.
## Query parameters
None.
## Request body
None.
## Validation rules
The target user must exist.
## Success response
`200 OK` with the number of sessions revoked.
## Error responses
`401 unauthorized`; `403 forbidden` or `csrf_validation_failed`; `404 not_found`; `429 rate_limited`.
## Example request
```http
DELETE /api/v1/admin/users/01900000-0000-7000-8000-000000000005/sessions
Authorization: Bearer <admin-token>
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"revokedCount":3}
```
## Security considerations
The action is audit-recorded. Previously issued access tokens remain valid for at most fifteen minutes because request validation is stateless.
## Related endpoints
[`Get user`](GetUser.md), [`Delete user`](DeleteUser.md).
