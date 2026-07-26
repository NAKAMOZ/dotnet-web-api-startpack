---
method: POST
route: /api/v1/email-verification/send
auth: required
---

# Send email verification

## Purpose
Queues a fresh verification message for the authenticated user's address.
## HTTP method
`POST`
## Route
`/api/v1/email-verification/send`
## Authentication requirements
Bearer JWT or access cookie. The `email-sending` IP and target-account limits apply.
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
The account must still require verification.
## Success response
`202 Accepted`; provider delivery is asynchronous.
## Error responses
`401 unauthorized`; `403 csrf_validation_failed`; `409 conflict`; `429 rate_limited`.
## Example request
```http
POST /api/v1/email-verification/send
Authorization: Bearer <access-token>
```
## Example response
```http
HTTP/1.1 202 Accepted
```
## Security considerations
Per-account limiting stops a rotating botnet from flooding one victim's mailbox; provider failure details are not exposed to clients.
## Related endpoints
[`Confirm email`](Confirm.md), [`Register`](../Auth/Register.md).
