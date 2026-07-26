---
method: POST
route: /api/v1/email-verification/confirm
auth: anonymous
---

# Confirm email

## Purpose
Consumes a single-use verification token and marks its address verified.
## HTTP method
`POST`
## Route
`/api/v1/email-verification/confirm`
## Authentication requirements
Anonymous; the emailed token is the flow credential.
## Authorization requirements
None.
## Request headers
`Content-Type: application/json`.
## Route parameters
None.
## Query parameters
None.
## Request body
`token`.
## Validation rules
The token is required, structurally bounded, unexpired, unused, and of the email-verification type.
## Success response
`200 OK` with verification status.
## Error responses
`400 validation_failed` or `invalid_token`; `429 rate_limited`.
## Example request
```http
POST /api/v1/email-verification/confirm
Content-Type: application/json

{"token":"<verification-token>"}
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"emailVerified":true}
```
## Security considerations
Only a token hash is stored. Consumption and the account update must be atomic so concurrent submissions cannot both succeed.
## Related endpoints
[`Send verification`](Send.md), [`Login`](../Auth/Login.md).
