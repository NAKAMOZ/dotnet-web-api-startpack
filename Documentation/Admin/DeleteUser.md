---
method: DELETE
route: /api/v1/admin/users/{userId}
auth: required
---

# Admin delete user

## Purpose
Deletes a user and every credential they hold.
## HTTP method
`DELETE`
## Route
`/api/v1/admin/users/{userId}`
## Authentication requirements
Bearer JWT, access cookie, or API key.
## Authorization requirements
Permission `users:delete:any`.
## Request headers
Cookie mode requires a valid CSRF header.
## Route parameters
`userId`: user GUID.
## Query parameters
None.
## Request body
None.
## Validation rules
The target user must exist and deletion must satisfy service-level administrative invariants.
## Success response
`204 No Content`.
## Error responses
`401 unauthorized`; `403 forbidden` or `csrf_validation_failed`; `404 not_found`; `429 rate_limited`.
## Example request
```http
DELETE /api/v1/admin/users/01900000-0000-7000-8000-000000000005
Authorization: Bearer <admin-token>
```
## Example response
```http
HTTP/1.1 204 No Content
```
## Security considerations
The service must audit deletion with a null subject and the deleted ID in metadata; writing after deletion would violate the audit foreign key.
## Related endpoints
[`Get user`](GetUser.md), [`Revoke user sessions`](RevokeUserSessions.md).
