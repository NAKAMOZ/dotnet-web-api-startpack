---
method: POST
route: /api/v1/password-reset/request
auth: anonymous
---

# Request password reset

## Purpose
Queues reset instructions when the supplied address belongs to an eligible account.
## HTTP method
`POST`
## Route
`/api/v1/password-reset/request`
## Authentication requirements
Anonymous. The `email-sending` IP and target-account limits apply.
## Authorization requirements
None.
## Request headers
`Content-Type: application/json`.
## Route parameters
None.
## Query parameters
None.
## Request body
`email`.
## Validation rules
The email must be syntactically valid and within the shared maximum length.
## Success response
`202 Accepted` for both present and absent accounts.
## Error responses
`400 validation_failed`; `429 rate_limited`.
## Example request
```http
POST /api/v1/password-reset/request
Content-Type: application/json

{"email":"user@example.com"}
```
## Example response
```http
HTTP/1.1 202 Accepted
```
## Security considerations
Status, body, timing, and externally observable side effects must not disclose whether the address is registered.
## Related endpoints
[`Confirm password reset`](Confirm.md), [`Login`](../Auth/Login.md).
