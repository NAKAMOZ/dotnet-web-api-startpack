---
method: POST
route: /api/v1/mfa/totp/confirm
auth: required
---

# Confirm TOTP

## Purpose
Verifies the pending authenticator and returns show-once recovery codes.
## HTTP method
`POST`
## Route
`/api/v1/mfa/totp/confirm`
## Authentication requirements
Bearer JWT or access cookie.
## Authorization requirements
Authentication alone.
## Request headers
`Content-Type: application/json`; cookie mode also requires CSRF.
## Route parameters
None.
## Query parameters
None.
## Request body
Six-digit `code`.
## Validation rules
The code must have the required format, fall within the tolerated time window, and not replay an accepted step.
## Success response
`200 OK` with newly generated recovery codes.
## Error responses
`400 validation_failed`; `401 unauthorized`; `403 csrf_validation_failed`; `409 conflict`; `429 rate_limited`.
## Example request
```http
POST /api/v1/mfa/totp/confirm
Authorization: Bearer <access-token>
Content-Type: application/json

{"code":"123456"}
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"recoveryCodes":["<show-once-code>"]}
```
## Security considerations
Recovery codes are returned once and stored only as hashes. The last accepted TOTP step is persisted to stop replay inside the tolerance window.
## Related endpoints
[`Enroll TOTP`](TotpEnroll.md), [`Regenerate recovery codes`](RegenerateRecoveryCodes.md).
