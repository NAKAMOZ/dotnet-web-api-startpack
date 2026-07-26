---
method: DELETE
route: /api/v1/sessions/{sessionId}
auth: required
---

# Revoke a session

## Purpose
Revokes one session owned by the caller.
## HTTP method
`DELETE`
## Route
`/api/v1/sessions/{sessionId}`
## Authentication requirements
Bearer JWT or access cookie.
## Authorization requirements
Authentication alone; ownership is enforced in the lookup.
## Request headers
Cookie mode requires a valid CSRF header.
## Route parameters
`sessionId`: GUID of the session to revoke.
## Query parameters
None.
## Request body
None.
## Validation rules
`sessionId` must be a GUID and belong to the caller.
## Success response
`204 No Content`.
## Error responses
`401 unauthorized`; `403 csrf_validation_failed`; `404 not_found`; `429 rate_limited`.
## Example request
```http
DELETE /api/v1/sessions/01900000-0000-7000-8000-000000000002
Authorization: Bearer <access-token>
```
## Example response
```http
HTTP/1.1 204 No Content
```
## Security considerations
A session owned by someone else is indistinguishable from an unknown ID, preventing cross-user existence disclosure.
## Related endpoints
[`List sessions`](ListSessions.md), [`Logout`](../Auth/Logout.md).
