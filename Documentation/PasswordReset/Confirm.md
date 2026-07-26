---
method: POST
route: /api/v1/password-reset/confirm
auth: anonymous
---

# Confirm password reset

## Purpose
Consumes a reset token, installs a new password, and revokes every session.
## HTTP method
`POST`
## Route
`/api/v1/password-reset/confirm`
## Authentication requirements
Anonymous; the reset token is the flow credential.
## Authorization requirements
None.
## Request headers
`Content-Type: application/json`.
## Route parameters
None.
## Query parameters
None.
## Request body
`token` and `newPassword`.
## Validation rules
The token is required and bounded; the new password uses the shared strength and deny-list policy.
## Success response
`204 No Content`.
## Error responses
`400 validation_failed` or `invalid_token`; `429 rate_limited`.
## Example request
```http
POST /api/v1/password-reset/confirm
Content-Type: application/json

{"token":"<reset-token>","newPassword":"a new correct horse battery staple"}
```
## Example response
```http
HTTP/1.1 204 No Content
```
## Security considerations
The token is single-use and hash-stored. Reset rotates the security stamp and revokes all sessions because compromise is the assumed case.
## Related endpoints
[`Request reset`](Request.md), [`Login`](../Auth/Login.md).
