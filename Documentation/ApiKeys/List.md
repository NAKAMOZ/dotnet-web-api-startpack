---
method: GET
route: /api/v1/api-keys
auth: required
---

# List API keys

## Purpose
Lists the caller's API-key metadata without secret material.
## HTTP method
`GET`
## Route
`/api/v1/api-keys`
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
The subject must be a valid user ID.
## Success response
`200 OK` with key IDs, prefixes, names, scopes, timestamps, and revocation state.
## Error responses
`401 unauthorized`; `429 rate_limited`.
## Example request
```http
GET /api/v1/api-keys
Authorization: Bearer <access-token>
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

[{"id":"01900000-0000-7000-8000-000000000003","prefix":"prefix","name":"CI"}]
```
## Security considerations
Full keys and hashes never leave storage. Prefixes are identifiers for management, not authentication secrets.
## Related endpoints
[`Create API key`](Create.md), [`Revoke API key`](Revoke.md).
