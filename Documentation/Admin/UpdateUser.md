---
method: PATCH
route: /api/v1/admin/users/{userId}
auth: required
---

# Admin update user

## Purpose
Changes allowed administrative account fields or clears a lockout.
## HTTP method
`PATCH`
## Route
`/api/v1/admin/users/{userId}`
## Authentication requirements
Bearer JWT, access cookie, or API key.
## Authorization requirements
Permission `users:write:any`.
## Request headers
`Content-Type: application/json`; cookie mode requires CSRF.
## Route parameters
`userId`: user GUID.
## Query parameters
None.
## Request body
Optional display name, verification state, and unlock instruction.
## Validation rules
At least one allowed field is supplied; display name is bounded. Passwords and imposed lockouts are not accepted.
## Success response
`200 OK` with updated administrative detail.
## Error responses
`400 validation_failed`; `401 unauthorized`; `403 forbidden` or `csrf_validation_failed`; `404 not_found`; `429 rate_limited`.
## Example request
```http
PATCH /api/v1/admin/users/01900000-0000-7000-8000-000000000005
Authorization: Bearer <admin-token>
Content-Type: application/json

{"clearLockout":true}
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"id":"01900000-0000-7000-8000-000000000005","failedLoginCount":0}
```
## Security considerations
Administrators cannot set a password they would know or manufacture a lockout. The audit catalog currently has no `admin_user_updated` event, an explicit owner-visible gap.
## Related endpoints
[`Get user`](GetUser.md), [`Delete user`](DeleteUser.md).
