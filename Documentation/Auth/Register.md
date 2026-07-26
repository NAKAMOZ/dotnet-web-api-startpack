---
method: POST
route: /api/v1/auth/register
auth: anonymous
---

# Register

## Purpose
Creates an unverified account and queues its verification message.
## HTTP method
`POST`
## Route
`/api/v1/auth/register`
## Authentication requirements
Anonymous. The `registration` policy limits each client IP.
## Authorization requirements
None.
## Request headers
`Content-Type: application/json`; `X-Auth-Transport` is not used because registration issues no session.
## Route parameters
None.
## Query parameters
None.
## Request body
`email`, `password`, and optional `displayName`.
## Validation rules
Email format and length, the shared password-strength policy, and a 100-character display-name maximum.
## Success response
`202 Accepted` with the registration acknowledgement.
## Error responses
`400 validation_failed`; `429 rate_limited`. Existing and new addresses must otherwise receive the same shape.
## Example request
```http
POST /api/v1/auth/register
Content-Type: application/json

{"email":"new@example.com","password":"correct horse battery staple","displayName":"New User"}
```
## Example response
```http
HTTP/1.1 202 Accepted
Content-Type: application/json

{"message":"If the address can be registered, verification instructions will be sent."}
```
## Security considerations
The response must not reveal whether the address already exists. Passwords are never logged and are Argon2id-hashed before storage.
## Related endpoints
[`Confirm email`](../EmailVerification/Confirm.md), [`Login`](Login.md).
