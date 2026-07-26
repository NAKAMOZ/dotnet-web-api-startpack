---
method: POST
route: /api/v1/auth/logout
auth: required
---

# Logout

## Purpose
Revokes the current session and clears authentication cookies.
## HTTP method
`POST`
## Route
`/api/v1/auth/logout`
## Authentication requirements
Bearer JWT, access cookie, or a session-backed authenticated principal.
## Authorization requirements
Authentication alone; only the current `sid` is affected.
## Request headers
Cookie-authenticated requests require the CSRF header matching the CSRF cookie.
## Route parameters
None.
## Query parameters
None.
## Request body
None.
## Validation rules
The authenticated principal must carry parseable user and session identifiers.
## Success response
`204 No Content`.
## Error responses
`401 unauthorized`; `403 csrf_validation_failed`; `429 rate_limited`.
## Example request
```http
POST /api/v1/auth/logout
Authorization: Bearer <access-token>
```
## Example response
```http
HTTP/1.1 204 No Content
```
## Security considerations
Revocation affects refresh immediately; an already-issued access token remains valid only until its short expiry.
## Related endpoints
[`Login`](Login.md), [`Revoke session`](../Sessions/RevokeSession.md).
