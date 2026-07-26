---
method: GET
route: /api/v1/users/me
auth: required
---

# Get my profile

## Purpose
Returns the authenticated caller's profile.
## HTTP method
`GET`
## Route
`/api/v1/users/me`
## Authentication requirements
Bearer JWT, access cookie, or API key.
## Authorization requirements
Authentication alone; the subject is always taken from the principal.
## Request headers
One supported authentication transport.
## Route parameters
None.
## Query parameters
None.
## Request body
None.
## Validation rules
The principal must carry a valid user ID.
## Success response
`200 OK` with email, verification state, display name, and timestamps.
## Error responses
`401 unauthorized`; `429 rate_limited`.
## Example request
```http
GET /api/v1/users/me
Authorization: Bearer <access-token>
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"email":"user@example.com","emailVerified":true,"displayName":"User"}
```
## Security considerations
There is no user-ID parameter, removing the cross-user lookup surface. Secret and lockout fields are not projected.
## Related endpoints
[`Update profile`](UpdateMe.md), [`Delete account`](DeleteMe.md).
