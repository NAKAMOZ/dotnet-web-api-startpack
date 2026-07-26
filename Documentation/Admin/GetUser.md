---
method: GET
route: /api/v1/admin/users/{userId}
auth: required
---

# Admin get user

## Purpose
Returns one user's administrative detail, including lockout and session summaries.
## HTTP method
`GET`
## Route
`/api/v1/admin/users/{userId}`
## Authentication requirements
Bearer JWT, access cookie, or API key.
## Authorization requirements
Permission `users:read:any`.
## Request headers
One supported authentication transport.
## Route parameters
`userId`: user GUID.
## Query parameters
None.
## Request body
None.
## Validation rules
`userId` must be a GUID and identify an existing user.
## Success response
`200 OK` with administrative detail.
## Error responses
`401 unauthorized`; `403 forbidden`; `404 not_found`; `429 rate_limited`.
## Example request
```http
GET /api/v1/admin/users/01900000-0000-7000-8000-000000000005
Authorization: Bearer <admin-token>
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"id":"01900000-0000-7000-8000-000000000005","email":"user@example.com","failedLoginCount":0}
```
## Security considerations
The endpoint exposes security state and therefore has a dedicated read-any permission; password and token material remain excluded.
## Related endpoints
[`List users`](ListUsers.md), [`Revoke user sessions`](RevokeUserSessions.md).
