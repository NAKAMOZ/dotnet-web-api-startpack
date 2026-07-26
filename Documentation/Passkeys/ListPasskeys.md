---
method: GET
route: /api/v1/passkeys
auth: required
---

# List passkeys

## Purpose
Lists safe metadata for the caller's registered credentials.
## HTTP method
`GET`
## Route
`/api/v1/passkeys`
## Authentication requirements
Bearer JWT or access cookie.
## Authorization requirements
Authentication alone.
## Request headers
`Authorization: Bearer <token>` or the access cookie.
## Route parameters
None.
## Query parameters
None.
## Request body
None.
## Validation rules
The authenticated subject must be a valid user ID.
## Success response
`200 OK` with an array of credential IDs, names, and timestamps.
## Error responses
`401 unauthorized`; `429 rate_limited`.
## Example request
```http
GET /api/v1/passkeys
Authorization: Bearer <access-token>
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

[{"credentialId":"<base64url>","name":"Laptop"}]
```
## Security considerations
No public key, attestation payload, or internal counter is returned; the query is fixed to the token subject.
## Related endpoints
[`Register passkey`](RegistrationOptions.md), [`Delete passkey`](DeletePasskey.md).
