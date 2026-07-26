---
method: POST
route: /api/v1/auth/login/mfa
auth: anonymous
---

# Complete MFA login

## Purpose
Exchanges a short-lived MFA ticket plus a TOTP or recovery code for a session.
## HTTP method
`POST`
## Route
`/api/v1/auth/login/mfa`
## Authentication requirements
Anonymous at HTTP level; the MFA ticket is the flow credential. `auth-strict` applies.
## Authorization requirements
None.
## Request headers
`Content-Type: application/json`; optional `X-Auth-Transport: cookie`.
## Route parameters
None.
## Query parameters
None.
## Request body
MFA ticket and exactly one supported second-factor code.
## Validation rules
Ticket and code are required and structurally bounded.
## Success response
`200 OK` with the new token pair or cookie-mode acknowledgement.
## Error responses
`400 validation_failed`; `401 invalid_credentials`; `429 rate_limited`.
## Example request
```http
POST /api/v1/auth/login/mfa
Content-Type: application/json

{"mfaTicket":"<ticket>","code":"123456"}
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"accessToken":"<access-token>","refreshToken":"<refresh-token>","expiresIn":900}
```
## Security considerations
Tickets are short-lived, audience-bound, and never accepted as access tokens. Recovery codes must be consumed once and TOTP replay must be rejected.
## Related endpoints
[`Login`](Login.md), [`Enroll TOTP`](../Mfa/TotpEnroll.md).
