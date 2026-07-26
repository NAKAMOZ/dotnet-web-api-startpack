---
method: POST
route: /api/v1/passkeys/authentication/complete
auth: anonymous
---

# Complete passkey authentication

## Purpose
Verifies a WebAuthn assertion and creates a session.
## HTTP method
`POST`
## Route
`/api/v1/passkeys/authentication/complete`
## Authentication requirements
Anonymous; the assertion completes the authentication ceremony. `auth-strict` applies.
## Authorization requirements
None.
## Request headers
`Content-Type: application/json`; optional `X-Auth-Transport: cookie`.
## Route parameters
None.
## Query parameters
None.
## Request body
Credential ID, authenticator data, client data, signature, and ceremony identifier.
## Validation rules
Encoded fields are required and bounded; challenge, origin, RP ID, flags, signature, and sign counter are verified.
## Success response
`200 OK` with a session token pair or cookie-mode acknowledgement.
## Error responses
`400 validation_failed`; `401 invalid_credentials`; `429 rate_limited`.
## Example request
```http
POST /api/v1/passkeys/authentication/complete
Content-Type: application/json

{"credentialId":"<base64url>","signature":"<base64url>","clientDataJson":"<base64url>"}
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"accessToken":"<access-token>","refreshToken":"<refresh-token>","expiresIn":900}
```
## Security considerations
Sign-counter rollback is treated as possible credential cloning. Failures do not reveal whether a credential ID exists.
## Related endpoints
[`Authentication options`](AuthenticationOptions.md), [`List sessions`](../Sessions/ListSessions.md).
