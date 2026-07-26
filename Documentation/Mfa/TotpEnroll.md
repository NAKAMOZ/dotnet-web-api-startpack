---
method: POST
route: /api/v1/mfa/totp/enroll
auth: required
---

# Enroll TOTP

## Purpose
Creates a pending authenticator secret and returns it once for setup.
## HTTP method
`POST`
## Route
`/api/v1/mfa/totp/enroll`
## Authentication requirements
Bearer JWT or access cookie.
## Authorization requirements
Authentication alone.
## Request headers
Cookie mode requires a valid CSRF header.
## Route parameters
None.
## Query parameters
None.
## Request body
None.
## Validation rules
No active confirmed TOTP credential may already exist.
## Success response
`200 OK` with the secret and provisioning URI.
## Error responses
`401 unauthorized`; `403 csrf_validation_failed`; `409 conflict`; `429 rate_limited`.
## Example request
```http
POST /api/v1/mfa/totp/enroll
Authorization: Bearer <access-token>
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"secret":"<base32-secret>","provisioningUri":"otpauth://totp/..."}
```
## Security considerations
The secret is show-once, encrypted at rest, and must not be audited or logged. Enrollment is not active until confirmation proves possession.
## Related endpoints
[`Confirm TOTP`](TotpConfirm.md), [`Disable TOTP`](TotpDisable.md).
