---
method: PUT
route: /api/v1/users/me/password
auth: required
---

# Change password

## Purpose
Replaces the caller's password and revokes all sessions.
## HTTP method
`PUT`
## Route
`/api/v1/users/me/password`
## Authentication requirements
Bearer JWT or access cookie.
## Authorization requirements
Authentication plus proof of the current password; recent-auth metadata is not additionally required.
## Request headers
`Content-Type: application/json`; cookie mode requires CSRF.
## Route parameters
None.
## Query parameters
None.
## Request body
`currentPassword` and `newPassword`.
## Validation rules
Both are required; the new value uses the shared password policy and must differ appropriately.
## Success response
`204 No Content`.
## Error responses
`400 validation_failed`; `401 invalid_credentials` or `unauthorized`; `403 csrf_validation_failed`; `429 rate_limited`.
## Example request
```http
PUT /api/v1/users/me/password
Authorization: Bearer <access-token>
Content-Type: application/json

{"currentPassword":"old password","newPassword":"new correct horse battery staple"}
```
## Example response
```http
HTTP/1.1 204 No Content
```
## Security considerations
Both password fields are excluded from logs. Success rotates the security stamp and revokes even the current session.
## Related endpoints
[`Password reset`](../PasswordReset/Request.md), [`Login`](../Auth/Login.md).
