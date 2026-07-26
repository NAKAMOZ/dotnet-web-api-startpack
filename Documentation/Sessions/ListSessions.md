---
method: GET
route: /api/v1/sessions
auth: required
---

# List sessions

## Purpose
Shows the caller's live sessions and marks the session making the request.
## HTTP method
`GET`
## Route
`/api/v1/sessions`
## Authentication requirements
Bearer JWT or access cookie.
## Authorization requirements
Authentication alone; the subject always comes from the token.
## Request headers
`Authorization: Bearer <token>` or the access cookie.
## Route parameters
None.
## Query parameters
None.
## Request body
None.
## Validation rules
The principal must carry a valid `sub` and `sid`.
## Success response
`200 OK` with an array of session summaries.
## Error responses
`401 unauthorized`; `429 rate_limited`.
## Example request
```http
GET /api/v1/sessions
Authorization: Bearer <access-token>
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

[{"id":"01900000-0000-7000-8000-000000000001","isCurrent":true}]
```
## Security considerations
Only the caller's rows are queried. User-agent and IP values are display hints, not trusted device identity.
## Related endpoints
[`Revoke session`](RevokeSession.md), [`Revoke other sessions`](RevokeAllSessions.md).
