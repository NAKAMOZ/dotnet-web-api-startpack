---
method: POST
route: /api/v1/mfa/recovery-codes/regenerate
auth: required
---

# Regenerate recovery codes

## Purpose
Invalidates all existing recovery codes and returns a new show-once set.
## HTTP method
`POST`
## Route
`/api/v1/mfa/recovery-codes/regenerate`
## Authentication requirements
Bearer JWT or access cookie from a recently authenticated session.
## Authorization requirements
The recent-authentication policy must succeed; API keys cannot satisfy it.
## Request headers
Cookie mode requires a valid CSRF header.
## Route parameters
None.
## Query parameters
None.
## Request body
None.
## Validation rules
The caller must have an active MFA enrollment.
## Success response
`200 OK` with the replacement codes.
## Error responses
`401 unauthorized`; `403 step_up_required` or `csrf_validation_failed`; `409 conflict`; `429 rate_limited`.
## Example request
```http
POST /api/v1/mfa/recovery-codes/regenerate
Authorization: Bearer <recent-access-token>
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"recoveryCodes":["<show-once-code>"]}
```
## Security considerations
Generation atomically invalidates the old set. Codes never appear again after this response and are stored using the high-entropy secret hash profile.
## Related endpoints
[`Confirm TOTP`](TotpConfirm.md), [`Disable TOTP`](TotpDisable.md).
