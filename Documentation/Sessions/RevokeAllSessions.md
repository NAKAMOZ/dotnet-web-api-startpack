---
method: DELETE
route: /api/v1/sessions
auth: required
---

# Revoke all other sessions

## Purpose
Revokes every session owned by the caller except the one making the request.
## HTTP method
`DELETE`
## Route
`/api/v1/sessions`
## Authentication requirements
Bearer JWT or access cookie.
## Authorization requirements
Authentication alone; scope is fixed to the caller.
## Request headers
Cookie mode requires a valid CSRF header.
## Route parameters
None.
## Query parameters
None.
## Request body
None.
## Validation rules
The current principal must carry a valid session ID to preserve.
## Success response
`200 OK` with the number of sessions revoked.
## Error responses
`401 unauthorized`; `403 csrf_validation_failed`; `429 rate_limited`.
## Example request
```http
DELETE /api/v1/sessions
Authorization: Bearer <access-token>
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"revokedCount":2}
```
## Security considerations
The current session is preserved deliberately so a security cleanup does not strand the user before they can inspect the result.
## Related endpoints
[`List sessions`](ListSessions.md), [`Revoke one session`](RevokeSession.md).
