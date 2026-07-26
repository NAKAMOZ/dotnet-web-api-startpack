---
method: DELETE
route: /api/v1/mfa/totp
auth: required
---

# Disable TOTP

## Purpose
Removes the caller's TOTP credential and recovery codes.
## HTTP method
`DELETE`
## Route
`/api/v1/mfa/totp`
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
An active TOTP enrollment must exist.
## Success response
`204 No Content`.
## Error responses
`401 unauthorized`; `403 step_up_required` or `csrf_validation_failed`; `404 not_found`; `429 rate_limited`.
## Example request
```http
DELETE /api/v1/mfa/totp
Authorization: Bearer <recent-access-token>
```
## Example response
```http
HTTP/1.1 204 No Content
```
## Security considerations
Disabling a second factor is a high-value post-session-theft action, so access-token validity alone is insufficient.
## Related endpoints
[`Enroll TOTP`](TotpEnroll.md), [`Regenerate recovery codes`](RegenerateRecoveryCodes.md).
