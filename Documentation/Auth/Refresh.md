---
method: POST
route: /api/v1/auth/refresh
auth: anonymous
---

# Refresh tokens

## Purpose
Rotates a valid refresh token and returns the next access/refresh pair.
## HTTP method
`POST`
## Route
`/api/v1/auth/refresh`
## Authentication requirements
Anonymous at HTTP level; the opaque refresh token is the credential. `auth-strict` applies.
## Authorization requirements
The token must belong to a live, unexpired session whose security-stamp snapshot is current.
## Request headers
`Content-Type: application/json`; cookie mode sends the path-scoped refresh cookie and a valid CSRF header.
## Route parameters
None.
## Query parameters
None.
## Request body
Body mode supplies `refreshToken`; cookie mode uses the secure cookie.
## Validation rules
At most one transport supplies a bounded non-empty token.
## Success response
`200 OK` with a newly rotated pair.
## Error responses
`400 validation_failed`; `401 invalid_token`; `429 rate_limited`.
## Example request
```http
POST /api/v1/auth/refresh
Content-Type: application/json

{"refreshToken":"<opaque-refresh-token>"}
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"accessToken":"<new-access-token>","refreshToken":"<new-refresh-token>","expiresIn":900}
```
## Security considerations
Rotation is single-use. Re-presenting a consumed token is treated as reuse and revokes the entire session chain.
## Related endpoints
[`Login`](Login.md), [`Logout`](Logout.md), [`List sessions`](../Sessions/ListSessions.md).
