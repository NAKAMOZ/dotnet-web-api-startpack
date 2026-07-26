---
method: PATCH
route: /api/v1/users/me
auth: required
---

# Update my profile

## Purpose
Changes the caller's display name.
## HTTP method
`PATCH`
## Route
`/api/v1/users/me`
## Authentication requirements
Bearer JWT, access cookie, or API key.
## Authorization requirements
Authentication alone.
## Request headers
`Content-Type: application/json`; cookie mode requires CSRF.
## Route parameters
None.
## Query parameters
None.
## Request body
Optional `displayName`.
## Validation rules
Display name is trimmed and limited to 100 characters.
## Success response
`200 OK` with the updated profile.
## Error responses
`400 validation_failed`; `401 unauthorized`; `403 csrf_validation_failed`; `429 rate_limited`.
## Example request
```http
PATCH /api/v1/users/me
Authorization: Bearer <access-token>
Content-Type: application/json

{"displayName":"Updated User"}
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"email":"user@example.com","displayName":"Updated User"}
```
## Security considerations
Email, roles, verification state, password, and lockout state are not writable through this self-service shape.
## Related endpoints
[`Get profile`](GetMe.md), [`Change password`](ChangePassword.md).
