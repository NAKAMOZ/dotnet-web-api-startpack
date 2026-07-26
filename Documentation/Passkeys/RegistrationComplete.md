---
method: POST
route: /api/v1/passkeys/registration/complete
auth: required
---

# Complete passkey registration

## Purpose
Verifies attestation and stores the caller's new public-key credential.
## HTTP method
`POST`
## Route
`/api/v1/passkeys/registration/complete`
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
The WebAuthn attestation response, client data, credential ID, and optional name.
## Validation rules
All encoded fields are required and bounded; origin, RP ID, challenge, flags, and algorithms are verified cryptographically.
## Success response
`201 Created` with safe credential metadata.
## Error responses
`400 validation_failed`; `401 unauthorized`; `403 csrf_validation_failed`; `409 conflict`; `429 rate_limited`.
## Example request
```http
POST /api/v1/passkeys/registration/complete
Authorization: Bearer <access-token>
Content-Type: application/json

{"credentialId":"<base64url>","clientDataJson":"<base64url>","attestationObject":"<base64url>"}
```
## Example response
```http
HTTP/1.1 201 Created
Content-Type: application/json

{"credentialId":"<base64url>","name":"Laptop"}
```
## Security considerations
Verification uses the stored challenge, never a challenge echoed by the client. Only public credential material is stored.
## Related endpoints
[`Registration options`](RegistrationOptions.md), [`Delete passkey`](DeletePasskey.md).
