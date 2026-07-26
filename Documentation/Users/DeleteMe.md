---
method: DELETE
route: /api/v1/users/me
auth: required
---

# Delete my account

## Purpose
Irreversibly deletes the caller and all credentials.
## HTTP method
`DELETE`
## Route
`/api/v1/users/me`
## Authentication requirements
Bearer JWT or access cookie from a recently authenticated session.
## Authorization requirements
Recent authentication is mandatory; API keys cannot satisfy it.
## Request headers
Cookie mode requires a valid CSRF header.
## Route parameters
None.
## Query parameters
None.
## Request body
None.
## Validation rules
The caller must exist and the `auth_time` claim must be inside the configured window.
## Success response
`204 No Content`.
## Error responses
`401 unauthorized`; `403 step_up_required` or `csrf_validation_failed`; `429 rate_limited`.
## Example request
```http
DELETE /api/v1/users/me
Authorization: Bearer <recent-access-token>
```
## Example response
```http
HTTP/1.1 204 No Content
```
## Security considerations
Recent authentication limits damage from a stolen session. Cascades remove every login path; the audit record must not keep a dangling user foreign key.
## Related endpoints
[`Get profile`](GetMe.md), [`Revoke sessions`](../Sessions/RevokeAllSessions.md).
