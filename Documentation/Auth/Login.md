---
method: POST
route: /api/v1/auth/login
auth: anonymous
---

# Login

## Purpose
Authenticates an email/password credential and returns tokens or an MFA challenge.
## HTTP method
`POST`
## Route
`/api/v1/auth/login`
## Authentication requirements
Anonymous. `auth-strict` allows 10 attempts per minute per client IP by default.
## Authorization requirements
None.
## Request headers
`Content-Type: application/json`; optional `X-Auth-Transport: cookie` selects secure-cookie delivery.
## Route parameters
None.
## Query parameters
None.
## Request body
`email` and `password`.
## Validation rules
Both fields are required and bounded; login deliberately does not re-grade password strength.
## Success response
`200 OK` with a token pair, or `202 Accepted` with an MFA ticket when a second factor is enrolled.
## Error responses
`400 validation_failed`; `401 invalid_credentials`; `429 rate_limited`.
## Example request
```http
POST /api/v1/auth/login
Content-Type: application/json

{"email":"user@example.com","password":"correct horse battery staple"}
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"accessToken":"<access-token>","refreshToken":"<refresh-token>","expiresIn":900}
```
## Security considerations
Unknown user, wrong password, and locked account must match in status, body, and timing. The account lock and IP limiter address different guessing patterns.
## Related endpoints
[`Complete MFA login`](LoginMfa.md), [`Refresh`](Refresh.md), [`CSRF token`](Csrf.md).
