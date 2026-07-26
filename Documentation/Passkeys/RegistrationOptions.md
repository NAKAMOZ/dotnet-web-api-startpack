---
method: POST
route: /api/v1/passkeys/registration/options
auth: required
---

# Passkey registration options

## Purpose
Starts a WebAuthn registration ceremony for the caller.
## HTTP method
`POST`
## Route
`/api/v1/passkeys/registration/options`
## Authentication requirements
Bearer JWT or access cookie.
## Authorization requirements
Authentication alone.
## Request headers
`Content-Type: application/json`; cookie mode requires CSRF.
## Route parameters
None.
## Query parameters
None.
## Request body
Optional human-readable credential name.
## Validation rules
The name is trimmed and length-bounded.
## Success response
`200 OK` with relying-party, user, challenge, algorithm, and authenticator-selection options.
## Error responses
`400 validation_failed`; `401 unauthorized`; `403 csrf_validation_failed`; `429 rate_limited`.
## Example request
```http
POST /api/v1/passkeys/registration/options
Authorization: Bearer <access-token>
Content-Type: application/json

{"name":"Laptop"}
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"challenge":"<base64url>","rp":{"id":"example.com","name":"Example"}}
```
## Security considerations
The challenge is random, short-lived, server-stored, and bound to the caller and ceremony type.
## Related endpoints
[`Complete registration`](RegistrationComplete.md), [`List passkeys`](ListPasskeys.md).
