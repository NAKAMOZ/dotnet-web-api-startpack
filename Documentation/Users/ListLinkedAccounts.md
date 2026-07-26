---
method: GET
route: /api/v1/users/me/accounts
auth: required
---

# List linked accounts

## Purpose
Lists social identities linked to the caller.
## HTTP method
`GET`
## Route
`/api/v1/users/me/accounts`
## Authentication requirements
Bearer JWT, access cookie, or API key.
## Authorization requirements
Authentication alone.
## Request headers
One supported authentication transport.
## Route parameters
None.
## Query parameters
None.
## Request body
None.
## Validation rules
The subject must be a valid user ID.
## Success response
`200 OK` with provider, account ID metadata, and timestamps.
## Error responses
`401 unauthorized`; `429 rate_limited`.
## Example request
```http
GET /api/v1/users/me/accounts
Authorization: Bearer <access-token>
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

[{"id":"01900000-0000-7000-8000-000000000004","provider":"google"}]
```
## Security considerations
Provider access and refresh tokens are never returned. Results are scoped to the authenticated subject.
## Related endpoints
[`Unlink account`](UnlinkAccount.md), [`Social login`](../SocialAuth/Authorize.md).
